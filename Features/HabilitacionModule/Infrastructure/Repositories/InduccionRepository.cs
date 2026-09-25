using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Inducciones;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Helpers;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Shared.Constants;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class InduccionRepository : IInduccionRepository
    {
        private const int ItemInduccionObra = 12;
        private const int ItemRegistroEpp = 5;
        private const int ItemRisst = 6;
        private const int ItemEntregaRecomendaciones = 8;
        private const int ItemDifusionPts = 10;

        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ITrabajadorRestringidoService _restringidoService;

        public InduccionRepository(
            IDbContextFactory<AppDbContext> factory,
            ITrabajadorRestringidoService restringidoService)
        {
            _factory = factory;
            _restringidoService = restringidoService;
        }

        public async Task<List<int>> CreateAsync(InduccionCreateDto dto, int programadoPor)
        {
            using var ctx = _factory.CreateDbContext();

            // Validar restricciones antes de programar
            foreach (var workerId in dto.WorkerIds)
            {
                var w = await ctx.Worker
                    .Include(x => x.Person)
                    .FirstOrDefaultAsync(x => x.Id == workerId)
                    ?? throw new AbrilException($"Trabajador {workerId} no encontrado.", 404);
                if (await _restringidoService.EstaRestringidoPorDniAsync(w.Person?.DocumentIdentityCode))
                    throw new AbrilException(
                        $"El trabajador {w.Person?.FullName} está restringido. Comuníquese con el área de Administración o SSOMA.", 400);
            }

            var limaZone = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
            var fechaLima = DateTime.SpecifyKind(dto.FechaProgramada, DateTimeKind.Unspecified);
            var fecha = TimeZoneInfo.ConvertTimeToUtc(fechaLima, limaZone);
            var hoyLima = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, limaZone).Date;

            // IDs de workers que ya tienen una inducción PROGRAMADA que sigue bloqueando el reagendado:
            // vigente (fecha aún no vencida) o ya con ingreso confirmado (asistió y está pendiente de
            // aprobación, no de reprogramación). Solo se libera cuando es un no-show real: vencida y
            // sin ingreso confirmado — sin depender de que el cron reset-falta ya la haya pasado a FALTA.
            var yaExisten = await ctx.SsInduccion
                .Where(i => dto.WorkerIds.Contains(i.WorkerId)
                    && i.ProyectoId == dto.ProyectoId
                    && i.Estado == "PROGRAMADA")
                .Select(i => new { i.WorkerId, i.FechaProgramada, i.IngresoConfirmado })
                .ToListAsync();

            var yaExistenVigentesSet = yaExisten
                .Where(i => i.IngresoConfirmado
                    || TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(i.FechaProgramada, DateTimeKind.Utc), limaZone).Date >= hoyLima)
                .Select(i => i.WorkerId)
                .ToHashSet();

            var nuevos = dto.WorkerIds.Distinct()
                .Where(wId => !yaExistenVigentesSet.Contains(wId))
                .ToList();

            if (nuevos.Count == 0) return [];

            // Cada inducción lleva la razón social de SU trabajador, la misma que la lista de
            // «Programar Inducción» le muestra. Antes llevaban todas la del primer seleccionado, y
            // un trabajador sin ninguna caía en empresa_id = 0: la FK a contributor lo rechazaba
            // con un 500 genérico (le pasó a un ingreso por carta oferta cuyo EMO se cargó con
            // «Registrar EMO» en vez de programarse, que es donde se elige la razón social).
            var empresas = await EmpresaEnProyectoAsync(ctx, nuevos, dto.ProyectoId);
            var sinRazonSocial = nuevos.Where(wId => empresas[wId] == null).ToList();
            if (sinRazonSocial.Count > 0)
            {
                var nombres = await ctx.Worker
                    .Where(w => sinRazonSocial.Contains(w.Id))
                    .Select(w => w.Person != null ? w.Person.FullName : null)
                    .ToListAsync();
                throw new AbrilException(sinRazonSocial.Count == 1
                    ? $"{nombres.FirstOrDefault()} no tiene razón social asignada, así que no se le puede programar la inducción."
                    : $"No tienen razón social asignada, así que no se les puede programar la inducción: {string.Join(", ", nombres)}.", 400);
            }

            var now = DateTime.UtcNow;

            var inducciones = nuevos.Select(wId => new SsInduccion
            {
                WorkerId = wId,
                ProyectoId = dto.ProyectoId,
                EmpresaId = empresas[wId]!.Value,
                FechaProgramada = fecha,
                TrabajoAltura = dto.TrabajoAltura,
                EquipoElectrico = dto.EquipoElectrico,
                Estado = "PROGRAMADA",
                ProgramadoPor = programadoPor,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();

            ctx.SsInduccion.AddRange(inducciones);
            await ctx.SaveChangesAsync();

            return inducciones.Select(i => i.Id).ToList();
        }

        public async Task<List<InduccionListDto>> GetAsync(
            int? proyectoId, int? empresaId, string? estado,
            DateTime? fechaDesde, DateTime? fechaHasta)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.SsInduccion.AsQueryable();

            if (proyectoId.HasValue)
                query = query.Where(i => i.ProyectoId == proyectoId.Value);
            if (empresaId.HasValue)
                query = query.Where(i => i.EmpresaId == empresaId.Value);
            if (!string.IsNullOrWhiteSpace(estado))
                query = query.Where(i => i.Estado == estado);
            var desde = DateTime.SpecifyKind(fechaDesde ?? DateTime.MinValue, DateTimeKind.Utc);
            var hasta = DateTime.SpecifyKind(fechaHasta ?? DateTime.MaxValue, DateTimeKind.Utc);
            query = query.Where(i => i.FechaProgramada >= desde && i.FechaProgramada <= hasta);

            var rows = await query
                .OrderByDescending(i => i.FechaProgramada)
                .ThenByDescending(i => i.Id)
                .ToListAsync();

            if (rows.Count == 0) return [];

            var workerIds = rows.Select(r => r.WorkerId).Distinct().ToList();
            var proyectoIds = rows.Select(r => r.ProyectoId).Distinct().ToList();
            var empresaIds = rows.Select(r => r.EmpresaId).Distinct().ToList();

            var workers = await ctx.Worker
                .Where(w => workerIds.Contains(w.Id))
                .Select(w => new
                {
                    w.Id,
                    ApellidoNombre = w.Person != null ? w.Person.FullName : null,
                    Dni = w.Person != null ? w.Person.DocumentIdentityCode : null
                })
                .ToDictionaryAsync(w => w.Id);

            var proyectos = await ctx.Project
                .Where(p => proyectoIds.Contains(p.ProjectId))
                .Select(p => new { p.ProjectId, p.ProjectDescription })
                .ToDictionaryAsync(p => p.ProjectId);

            var empresas = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .Select(c => new { c.ContributorId, c.ContributorName })
                .ToDictionaryAsync(c => c.ContributorId);

            var confirmadoPorIds = rows
                .Where(r => r.ConfirmadoPorUserId.HasValue)
                .Select(r => r.ConfirmadoPorUserId!.Value)
                .Distinct()
                .ToList();
            var usuarioMap = await ctx.User
                .Include(u => u.Person)
                .Where(u => confirmadoPorIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.Person != null ? u.Person.FullName : u.Email);

            return rows.Select(r =>
            {
                workers.TryGetValue(r.WorkerId, out var w);
                proyectos.TryGetValue(r.ProyectoId, out var p);
                empresas.TryGetValue(r.EmpresaId, out var e);
                string? confirmadoPorNombre = null;
                if (r.ConfirmadoPorUserId.HasValue)
                    usuarioMap.TryGetValue(r.ConfirmadoPorUserId.Value, out confirmadoPorNombre);

                return new InduccionListDto
                {
                    Id = r.Id,
                    WorkerId = r.WorkerId,
                    ApellidoNombre = w?.ApellidoNombre ?? string.Empty,
                    Dni = w?.Dni ?? string.Empty,
                    ProyectoId = r.ProyectoId,
                    ProyectoNombre = p?.ProjectDescription ?? string.Empty,
                    EmpresaId = r.EmpresaId,
                    EmpresaNombre = e?.ContributorName ?? string.Empty,
                    FechaProgramada = r.FechaProgramada,
                    TrabajoAltura = r.TrabajoAltura,
                    EquipoElectrico = r.EquipoElectrico,
                    Estado = r.Estado,
                    IngresoConfirmado = r.IngresoConfirmado,
                    FechaIngreso = r.FechaIngreso,
                    ConfirmadoPorNombre = confirmadoPorNombre
                };
            }).ToList();
        }

        public async Task<List<InduccionTrabajadorDto>> GetTrabajadoresPorProgramarAsync(int? empresaId, int proyectoId, string? search = null)
        {
            using var ctx = _factory.CreateDbContext();

            // Workers asignados al proyecto con inducción pendiente (sin filtro fecha_fin)
            var workerIds = await ctx.WorkerProyecto
                .Where(wp => wp.ProyectoId == proyectoId && !wp.InduccionCompletada)
                .Select(wp => wp.WorkerId)
                .Distinct()
                .ToListAsync();

            if (workerIds.Count == 0) return [];

            var limaZone = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
            var hoyLima = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, limaZone).Date;

            // Solo bloquea una PROGRAMADA vigente (fecha >= hoy) o con ingreso ya confirmado (asistió,
            // está pendiente de aprobación, no de reprogramación). Una PROGRAMADA vencida sin ingreso
            // confirmado es un no-show real y no bloquea, aunque el cron reset-falta aún no la haya
            // pasado a FALTA.
            var workerIdsConProgramacion = (await ctx.SsInduccion
                .Where(i => i.ProyectoId == proyectoId
                         && workerIds.Contains(i.WorkerId)
                         && i.Estado == "PROGRAMADA")
                .Select(i => new { i.WorkerId, i.FechaProgramada, i.IngresoConfirmado })
                .ToListAsync())
                .Where(i => i.IngresoConfirmado
                    || TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(i.FechaProgramada, DateTimeKind.Utc), limaZone).Date >= hoyLima)
                .Select(i => i.WorkerId)
                .Distinct()
                .ToList();

            workerIds = workerIds
                .Where(id => !workerIdsConProgramacion.Contains(id))
                .ToList();

            if (workerIds.Count == 0) return [];

            // Si se filtra por empresa, reducir a workers con vinculación con esa empresa
            if (empresaId.HasValue)
            {
                var conEmpresa = await ctx.WorkerVinculacion
                    .Where(v => workerIds.Contains(v.WorkerId) && v.EmpresaId == empresaId.Value)
                    .Select(v => v.WorkerId)
                    .Distinct()
                    .ToListAsync();
                workerIds = conEmpresa;
                if (workerIds.Count == 0) return [];
            }

            // Datos del worker con filtro de búsqueda aplicado en la query
            var workersQuery = ctx.Worker.Where(w => workerIds.Contains(w.Id));

            // Sin empresa explícita, el llamador es Abril (staff en Trabajadores) abriendo
            // "Programar Inducción" sin haber preseleccionado trabajadores primero — en ese caso
            // solo debe ver personal de Casa; ver contratistas mezclados ahí fue el bug reportado.
            // Cuando SÍ llega empresaId (preselección de trabajadores de una empresa puntual, o
            // el propio contratista que siempre manda el suyo — ver ProgramarInduccion del portal
            // contratista) ya quedó filtrado arriba, así que este filtro extra no aplica.
            if (!empresaId.HasValue)
                workersQuery = workersQuery.Where(w => w.ContrataCasa == "Casa");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                if (s.Length == 8 && s.All(char.IsDigit))
                    workersQuery = workersQuery.Where(w => w.Person != null && w.Person.DocumentIdentityCode == s);
                else
                    workersQuery = workersQuery.Where(w =>
                        w.Person != null && w.Person.FullName != null && w.Person.FullName.ToLower().Contains(s.ToLower()));
            }

            var workers = await workersQuery
                .Select(w => new
                {
                    w.Id,
                    ApellidoNombre = w.Person != null ? w.Person.FullName : null,
                    Dni = w.Person != null ? w.Person.DocumentIdentityCode : null,
                    w.ObraOficinaStaffId
                })
                .ToDictionaryAsync(w => w.Id);

            workerIds = workers.Keys.ToList();
            if (workerIds.Count == 0) return [];

            // Razón social de cada worker: la misma con la que se le guarda la inducción.
            var empresaPorWorker = await EmpresaEnProyectoAsync(ctx, workerIds, proyectoId);

            var empresaIds = empresaPorWorker.Values
                .Where(e => e.HasValue)
                .Select(e => e!.Value)
                .Distinct()
                .ToList();

            var empresaMap = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId, c => c.ContributorName);

            // Badge futuro: workers que ya indujeron en este proyecto
            var yaIndujeroSet = (await ctx.WorkerProyecto
                .Where(wp => wp.ProyectoId == proyectoId && wp.InduccionCompletada)
                .Select(wp => wp.WorkerId)
                .ToListAsync()).ToHashSet();

            return workerIds
                .Where(workers.ContainsKey)
                .Select(wId =>
                {
                    var w = workers[wId];
                    var empId = empresaPorWorker[wId];
                    empresaMap.TryGetValue(empId ?? 0, out var empNombre);
                    return new InduccionTrabajadorDto
                    {
                        WorkerId = wId,
                        ApellidoNombre = w.ApellidoNombre ?? string.Empty,
                        Dni = w.Dni ?? string.Empty,
                        ObraOficinaStaffId = w.ObraOficinaStaffId,
                        ObraOficina = ObraOficinaStaffIds.Nombre(w.ObraOficinaStaffId),
                        EmpresaId = empId,
                        EmpresaNombre = empNombre ?? string.Empty,
                        YaIndujo = yaIndujeroSet.Contains(wId)
                    };
                })
                .OrderBy(d => d.ApellidoNombre)
                .ToList();
        }

        public async Task AprobarAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var induccion = await ctx.SsInduccion.FirstOrDefaultAsync(i => i.Id == id)
                ?? throw new AbrilException("Inducción no encontrada.", 404);

            await AprobarInduccionAsync(ctx, induccion);
            await ctx.SaveChangesAsync();
        }

        public async Task AprobarBatchAsync(List<int> ids)
        {
            using var ctx = _factory.CreateDbContext();

            var inducciones = await ctx.SsInduccion
                .Where(i => ids.Contains(i.Id))
                .ToListAsync();

            foreach (var induccion in inducciones)
                await AprobarInduccionAsync(ctx, induccion);

            await ctx.SaveChangesAsync();
        }

        public async Task RechazarAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            var induccion = await ctx.SsInduccion.FirstOrDefaultAsync(i => i.Id == id)
                ?? throw new AbrilException("Inducción no encontrada.", 404);

            // RECHAZADA no bloquea el reagendado (a diferencia de PROGRAMADA vigente o con
            // ingreso confirmado): el trabajador queda libre para volver a programarse.
            induccion.Estado = "RECHAZADA";
            induccion.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<int> ResetFaltaAsync()
        {
            using var ctx = _factory.CreateDbContext();
            var ahora = DateTime.UtcNow;
            var limaZone = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
            var hoyLima = TimeZoneInfo.ConvertTimeFromUtc(ahora, limaZone).Date;

            var candidatas = await ctx.SsInduccion
                .Where(i => i.Estado == "PROGRAMADA" && !i.IngresoConfirmado)
                .ToListAsync();

            var aMarcar = candidatas.Where(i =>
            {
                var fechaLima = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(i.FechaProgramada, DateTimeKind.Utc),
                    limaZone).Date;
                return fechaLima < hoyLima;
            }).ToList();

            foreach (var ind in aMarcar)
            {
                ind.Estado = "FALTA";
                ind.UpdatedAt = ahora;
            }

            await ctx.SaveChangesAsync();
            return aMarcar.Count;
        }

        private async Task AprobarInduccionAsync(AppDbContext ctx, SsInduccion induccion)
        {
            induccion.Estado = "REALIZADA";
            induccion.UpdatedAt = DateTime.UtcNow;

            // Determinar si el worker pertenece a empresa Casa
            var esAbril = await ctx.Contributor
                .Where(c => c.ContributorId == induccion.EmpresaId)
                .Select(c => c.EsAbril)
                .FirstOrDefaultAsync();

            // Items a aprobar: InduccionObra siempre + items Casa si corresponde
            var itemIds = new HashSet<int> { ItemInduccionObra };
            if (esAbril)
            {
                itemIds.Add(ItemRegistroEpp);
                itemIds.Add(ItemRisst);
                itemIds.Add(ItemEntregaRecomendaciones);
                itemIds.Add(ItemDifusionPts);
            }

            var now = DateTime.UtcNow;
            var sentinel = HabilitacionDateHelper.ResolverVigencia(false, "Aprobado", null);

            // Actualizar ss_hab_trabajador para cada item
            var habs = await ctx.SsHabTrabajador
                .Where(h => h.WorkerId == induccion.WorkerId && itemIds.Contains(h.ItemId))
                .ToListAsync();

            var habItemIds = habs.Select(h => h.ItemId).ToHashSet();

            foreach (var itemId in itemIds)
            {
                var hab = habs.FirstOrDefault(h => h.ItemId == itemId);
                if (hab is not null)
                {
                    hab.Estado = "Aprobado";
                    hab.Vigencia = sentinel;
                    hab.UpdatedAt = now;
                }
                else if (!habItemIds.Contains(itemId))
                {
                    ctx.SsHabTrabajador.Add(new SsHabTrabajador
                    {
                        WorkerId = induccion.WorkerId,
                        ItemId = itemId,
                        Estado = "Aprobado",
                        Vigencia = sentinel,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            // Marcar InduccionCompletada en ss_hab_worker_proyecto
            var workerProyecto = await ctx.WorkerProyecto
                .Where(wp => wp.WorkerId == induccion.WorkerId
                    && wp.ProyectoId == induccion.ProyectoId)
                .OrderByDescending(wp => wp.CreatedAt)
                .ThenByDescending(wp => wp.Id)
                .FirstOrDefaultAsync();

            if (workerProyecto is not null)
            {
                workerProyecto.InduccionCompletada = true;
                workerProyecto.FechaInduccion = DateOnly.FromDateTime(DateTime.UtcNow);
                workerProyecto.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        /// <summary>
        /// La razón social con la que cada trabajador se induce en <paramref name="proyectoId"/>:
        /// la de su asignación a ESE proyecto y, si no la tiene, la de su última vinculación. Null
        /// si no tiene ninguna. La lista de «Programar Inducción» la muestra y
        /// <see cref="CreateAsync"/> la guarda, así que las dos salen de acá.
        ///
        /// <para>Primero la del proyecto (multi-proyecto Casa): un worker asignado aquí con una
        /// empresa distinta a la de su vinculación principal, en otro proyecto, mostraba la
        /// equivocada. Y puede tener más de una fila para el MISMO proyecto (doble asignación, p. ej.
        /// registrada por dos contratas o por error; visto en Cedro 33 con proyecto_id = 8), así
        /// que un ToDictionary directo revienta con "An item with the same key has already been
        /// added": se agrupa y manda la más reciente, igual que en la vinculación.</para>
        /// </summary>
        private static async Task<Dictionary<int, int?>> EmpresaEnProyectoAsync(
            AppDbContext ctx, List<int> workerIds, int proyectoId)
        {
            var enProyecto = (await ctx.WorkerProyecto
                .Where(wp => wp.ProyectoId == proyectoId && workerIds.Contains(wp.WorkerId) && wp.EmpresaId.HasValue)
                .Select(wp => new { wp.Id, wp.WorkerId, wp.EmpresaId })
                .ToListAsync())
                .GroupBy(wp => wp.WorkerId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(wp => wp.Id).First().EmpresaId);

            var enUltimaVinculacion = (await ctx.WorkerVinculacion
                .Where(v => workerIds.Contains(v.WorkerId))
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .Select(v => new { v.WorkerId, v.EmpresaId })
                .ToListAsync())
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First().EmpresaId);

            return workerIds.Distinct().ToDictionary(
                id => id,
                id => enProyecto.TryGetValue(id, out var empresa)
                    ? empresa
                    : enUltimaVinculacion.GetValueOrDefault(id));
        }
    }
}
