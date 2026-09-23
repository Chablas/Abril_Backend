using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.ControlAcceso;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Features.Habilitacion.Infrastructure.Helpers;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class ControlAccesoRepository : IControlAccesoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IConfiguration _configuration;
        private readonly ITrabajadorRestringidoService _restringidoService;
        private const int ItemSctr = 11;

        public ControlAccesoRepository(
            IDbContextFactory<AppDbContext> factory,
            IConfiguration configuration,
            ITrabajadorRestringidoService restringidoService)
        {
            _factory = factory;
            _restringidoService = restringidoService;
            _configuration = configuration;
        }

        public async Task<List<ControlAccesoWorkerDto>> GetConsultaAsync(string? search, int? proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Worker.Include(w => w.Person)
                .Include(w => w.PuestoCatalogo).ThenInclude(p => p!.Categoria)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                var esDni = s.Length == 8 && s.All(char.IsDigit);
                if (esDni)
                    query = query.Where(w => w.Person != null && w.Person.DocumentIdentityCode == s);
                else
                    query = query.Where(w => w.Person != null && w.Person.FullName != null && w.Person.FullName.ToLower().Contains(s.ToLower()));
            }

            if (proyectoId.HasValue)
            {
                // Une vinculación laboral (worker_vinculaciones, 1 activa a la vez) con
                // asignaciones de apoyo a proyectos adicionales (ss_hab_worker_proyecto) —
                // mismo criterio que GetNoAutorizadosAsync, para no perder a alguien que
                // apoya un proyecto sin que sea su vinculación principal.
                var idsVinc = await ctx.WorkerVinculacion
                    .Where(v => v.ProyectoId == proyectoId.Value && v.FechaFin == null)
                    .Select(v => v.WorkerId)
                    .ToListAsync();
                var idsProyecto = await ctx.WorkerProyecto
                    .Where(wp => wp.ProyectoId == proyectoId.Value && wp.FechaFin == null)
                    .Select(wp => wp.WorkerId)
                    .ToListAsync();
                var ids = idsVinc.Union(idsProyecto).Distinct().ToList();
                query = query.Where(w => ids.Contains(w.Id));
            }

            var oficinaId = _configuration.GetValue<int>("OficinaCentral:ProjectId", 36);
            var esOficinaCentral = proyectoId.HasValue && proyectoId.Value == oficinaId;

            var workers = await query.Take(100).ToListAsync();
            return await BuildDtosAsync(ctx, workers, esOficinaCentral, proyectoId);
        }

        public async Task<List<ControlAccesoWorkerDto>> GetNoAutorizadosAsync(int proyectoId, string? estadoHabilitacion)
        {
            using var ctx = _factory.CreateDbContext();

            var idsVinc = await ctx.WorkerVinculacion
                .Where(v => v.ProyectoId == proyectoId && v.FechaFin == null)
                .Select(v => v.WorkerId)
                .ToListAsync();

            var idsProyecto = await ctx.WorkerProyecto
                .Where(wp => wp.ProyectoId == proyectoId && wp.FechaFin == null)
                .Select(wp => wp.WorkerId)
                .ToListAsync();

            var workerIds = idsVinc.Union(idsProyecto).Distinct().ToList();

            // Comparación por día calendario, no por instante: Vigencia guarda el ÚLTIMO día
            // válido del documento (medianoche UTC). Comparar contra DateTime.UtcNow con "<="
            // marcaba un documento vigente "hasta el 31" como vencido desde las 19:00 hora Lima
            // del día 30 (mismo bug que en el frontend, ver estaVencido() en trabajadores.ts) —
            // seguía "No Autorizado" durante todo el día 31 en vez de recién el 1. Con "<" contra
            // el día de hoy sigue vigente durante todo su último día.
            var hoy = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

            var workersInfo = await ctx.Worker
                .Where(w => workerIds.Contains(w.Id))
                .Select(w => new
                {
                    w.Id,
                    w.ContrataCasa,
                    w.ObraOficinaStaffId,
                    CategoriaNombre = w.PuestoCatalogo != null && w.PuestoCatalogo.Categoria != null
                        ? w.PuestoCatalogo.Categoria.Nombre : null
                })
                .ToListAsync();
            var workerInfoMap = workersInfo.ToDictionary(w => w.Id);

            var itemsFullMap = await ctx.SsItemTrabajador.ToDictionaryAsync(i => i.Id);

            var habRowsPendientes = await ctx.SsHabTrabajador
                .Where(h => workerIds.Contains(h.WorkerId) &&
                            (h.Estado == "Falta" || h.Estado == "Rechazado" || h.Estado == "Vencido" || h.Estado == "Enviado" ||
                             (h.Estado == "Aprobado" && (!h.Vigencia.HasValue || h.Vigencia.Value < hoy)) ||
                             (h.Estado == "Renovando" && (!h.Vigencia.HasValue || h.Vigencia.Value < hoy))))
                .Select(h => new { h.WorkerId, h.ItemId })
                .ToListAsync();

            // Mismo criterio de aplicabilidad (categoría/obra-oficina) que BuildDtosAsync — ver
            // EsItemAplicable. Sin esto, un ítem "Falta" que ya no le corresponde al trabajador
            // actual (categoría u obra distinta a cuando se creó la fila) lo marcaba "No
            // Autorizado" acá aunque Gestión de Ingresos ya no lo contara como pendiente.
            var noAutorizadosIds = habRowsPendientes
                .Where(h =>
                {
                    workerInfoMap.TryGetValue(h.WorkerId, out var info);
                    var esCasa = string.Equals(info?.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase);
                    var esContratista = string.Equals(info?.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);
                    var workerType = esCasa ? "CASA" : "CONTRATISTA";
                    var obraOficinaNombre = ObraOficinaStaffIds.Nombre(info?.ObraOficinaStaffId);
                    return EsItemAplicable(itemsFullMap, h.ItemId, workerType, info?.CategoriaNombre, obraOficinaNombre, esContratista);
                })
                .Select(h => h.WorkerId)
                .Distinct()
                .ToList();

            var noAutorizadosSet = noAutorizadosIds.ToHashSet();

            List<int> filteredIds = estadoHabilitacion switch
            {
                "No Autorizado" => noAutorizadosIds,
                "Habilitado"    => workerIds.Where(id => !noAutorizadosSet.Contains(id)).ToList(),
                _               => workerIds
            };

            if (filteredIds.Count == 0) return [];

            var workers = await ctx.Worker
                .Include(w => w.Person)
                .Include(w => w.PuestoCatalogo).ThenInclude(p => p!.Categoria)
                .Where(w => filteredIds.Contains(w.Id))
                .ToListAsync();

            return await BuildDtosAsync(ctx, workers, proyectoId: proyectoId);
        }

        public async Task<List<ControlAccesoWorkerDto>> GetOficinaCentralAsync(int? proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.Worker.Where(w =>
                w.ObraOficinaStaffId == ObraOficinaStaffIds.OficinaCentral ||
                w.ObraOficinaStaffId == ObraOficinaStaffIds.Staff);

            if (proyectoId.HasValue)
            {
                // Une vinculación laboral (worker_vinculaciones, 1 activa a la vez) con
                // asignaciones de apoyo a proyectos adicionales (ss_hab_worker_proyecto) —
                // mismo criterio que GetNoAutorizadosAsync, para no perder a alguien que
                // apoya un proyecto sin que sea su vinculación principal.
                var idsVinc = await ctx.WorkerVinculacion
                    .Where(v => v.ProyectoId == proyectoId.Value && v.FechaFin == null)
                    .Select(v => v.WorkerId)
                    .ToListAsync();
                var idsProyecto = await ctx.WorkerProyecto
                    .Where(wp => wp.ProyectoId == proyectoId.Value && wp.FechaFin == null)
                    .Select(wp => wp.WorkerId)
                    .ToListAsync();
                var ids = idsVinc.Union(idsProyecto).Distinct().ToList();
                query = query.Where(w => ids.Contains(w.Id));
            }

            var candidatos = await query.Select(w => w.Id).ToListAsync();

            var hoy = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            var conSctrIds = await ctx.SsHabTrabajador
                .Where(h => candidatos.Contains(h.WorkerId) &&
                            h.ItemId == ItemSctr &&
                            h.Estado == "Aprobado" &&
                            h.Vigencia >= hoy)
                .Select(h => h.WorkerId)
                .Distinct()
                .ToListAsync();

            var workers = await ctx.Worker
                .Include(w => w.Person)
                .Include(w => w.PuestoCatalogo).ThenInclude(p => p!.Categoria)
                .Where(w => conSctrIds.Contains(w.Id))
                .ToListAsync();

            return await BuildDtosAsync(ctx, workers, proyectoId: proyectoId);
        }

        public async Task<List<InduccionHoyDto>> GetInduccionesHoyAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // fecha_programada se almacena en hora local Lima (UTC-5)
            var hoyLima = DateTime.UtcNow.AddHours(-5).Date;
            var fechaLimite = hoyLima.AddDays(1);
            var inducciones = await ctx.SsInduccion
                .Where(i => i.Estado == "PROGRAMADA" &&
                            i.FechaProgramada >= hoyLima &&
                            i.FechaProgramada < fechaLimite)
                .ToListAsync();

            var workerIds = inducciones.Select(i => i.WorkerId).Distinct().ToList();
            var empresaIds = inducciones.Select(i => i.EmpresaId).Distinct().ToList();

            var workerMap = await ctx.Worker
                .Include(w => w.Person)
                .Where(w => workerIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id);

            var empresaMap = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId, c => c.ContributorName);

            var confirmadoPorIds = inducciones
                .Where(i => i.ConfirmadoPorUserId.HasValue)
                .Select(i => i.ConfirmadoPorUserId!.Value)
                .Distinct()
                .ToList();
            var usuarioMap = await ctx.User
                .Include(u => u.Person)
                .Where(u => confirmadoPorIds.Contains(u.UserId))
                .ToDictionaryAsync(u => u.UserId, u => u.Person != null ? u.Person.FullName : u.Email);

            return inducciones.Select(i =>
            {
                workerMap.TryGetValue(i.WorkerId, out var w);
                empresaMap.TryGetValue(i.EmpresaId, out var empNombre);
                string? confirmadoPorNombre = null;
                if (i.ConfirmadoPorUserId.HasValue)
                    usuarioMap.TryGetValue(i.ConfirmadoPorUserId.Value, out confirmadoPorNombre);

                return new InduccionHoyDto
                {
                    InduccionId = i.Id,
                    WorkerId = i.WorkerId,
                    ApellidoNombre = w?.Person?.FullName ?? "",
                    Dni = w?.Person?.DocumentIdentityCode ?? "",
                    EmpresaNombre = empNombre ?? "",
                    FechaProgramada = i.FechaProgramada,
                    TrabajoAltura = i.TrabajoAltura,
                    EquipoElectrico = i.EquipoElectrico,
                    Estado = i.Estado,
                    IngresoConfirmado = i.IngresoConfirmado,
                    FechaIngreso = i.FechaIngreso,
                    ConfirmadoPorNombre = confirmadoPorNombre
                };
            }).ToList();
        }

        public async Task ConfirmarIngresoAsync(int induccionId, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var induccion = await ctx.SsInduccion.FirstOrDefaultAsync(i => i.Id == induccionId)
                ?? throw new AbrilException("Inducción no encontrada.", 404);

            // Verificar que el trabajador no está en lista negra antes de confirmar ingreso físico
            var dni = await ctx.Worker
                .Where(w => w.Id == induccion.WorkerId)
                .Select(w => w.Person != null ? w.Person.DocumentIdentityCode : null)
                .FirstOrDefaultAsync();

            if (await _restringidoService.EstaRestringidoPorDniAsync(dni))
                throw new AbrilException("Trabajador restringido. No puede ingresar al proyecto.", 403);

            induccion.IngresoConfirmado = true;
            induccion.FechaIngreso = DateTime.UtcNow;
            induccion.ConfirmadoPorUserId = userId;
            induccion.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task DesconfirmarIngresoAsync(int induccionId)
        {
            using var ctx = _factory.CreateDbContext();

            var induccion = await ctx.SsInduccion.FirstOrDefaultAsync(i => i.Id == induccionId)
                ?? throw new AbrilException("Inducción no encontrada.", 404);

            induccion.IngresoConfirmado = false;
            induccion.FechaIngreso = null;
            induccion.ConfirmadoPorUserId = null;
            induccion.UpdatedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
        }

        public async Task<List<TareoPartidaDto>> GetPartidasAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsTareoPartida
                .Where(p => p.Activo)
                .OrderBy(p => p.Orden)
                .Select(p => new TareoPartidaDto { Id = p.Id, Nombre = p.Nombre })
                .ToListAsync();
        }

        public async Task<List<TareoEmpresaDto>> GetEmpresasContratistasByProyectoAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var empresaIdsVinc = await ctx.WorkerVinculacion
                .Where(v => v.ProyectoId == proyectoId && v.FechaFin == null && v.EmpresaId.HasValue)
                .Select(v => v.EmpresaId!.Value)
                .ToListAsync();

            var empresaIdsProyecto = await ctx.WorkerProyecto
                .Where(wp => wp.ProyectoId == proyectoId && wp.FechaFin == null && wp.EmpresaId.HasValue)
                .Select(wp => wp.EmpresaId!.Value)
                .ToListAsync();

            var empresaIds = empresaIdsVinc.Union(empresaIdsProyecto).Distinct().ToList();

            return await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId) && !c.EsAbril)
                .OrderBy(c => c.ContributorName)
                .Select(c => new TareoEmpresaDto { EmpresaId = c.ContributorId, EmpresaNombre = c.ContributorName })
                .ToListAsync();
        }

        public async Task<TareoDto?> GetTareoAsync(int proyectoId, DateOnly fecha)
        {
            using var ctx = _factory.CreateDbContext();

            var tareo = await ctx.SsTareo
                .Include(t => t.Proyecto)
                .FirstOrDefaultAsync(t => t.ProyectoId == proyectoId && t.Fecha == fecha);

            if (tareo == null) return null;

            var (casa, contratista) = await LoadDetallesAsync(ctx, tareo.Id);
            return MapTareoDto(tareo, casa, contratista);
        }

        public async Task<TareoDto> CreateTareoAsync(TareoCreateDto dto, int? userId)
        {
            using var ctx = _factory.CreateDbContext();

            var existe = await ctx.SsTareo
                .AnyAsync(t => t.ProyectoId == dto.ProyectoId && t.Fecha == dto.Fecha);
            if (existe)
                throw new AbrilException("Ya existe un tareo para este proyecto y fecha.", 409);

            var tareo = new SsTareo
            {
                ProyectoId = dto.ProyectoId,
                Fecha = dto.Fecha,
                Observaciones = dto.Observaciones,
                CreadoPor = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            ctx.SsTareo.Add(tareo);
            await ctx.SaveChangesAsync();

            InsertDetalles(ctx, tareo.Id, dto);
            await ctx.SaveChangesAsync();

            await ctx.Entry(tareo).Reference(t => t.Proyecto).LoadAsync();
            var (casa, contratista) = await LoadDetallesAsync(ctx, tareo.Id);
            return MapTareoDto(tareo, casa, contratista);
        }

        public async Task<TareoDto> UpdateTareoAsync(int id, TareoCreateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var tareo = await ctx.SsTareo
                .Include(t => t.Proyecto)
                .FirstOrDefaultAsync(t => t.Id == id)
                ?? throw new AbrilException("Tareo no encontrado.", 404);

            tareo.ProyectoId = dto.ProyectoId;
            tareo.Fecha = dto.Fecha;
            tareo.Observaciones = dto.Observaciones;
            tareo.UpdatedAt = DateTime.UtcNow;

            var oldCasa = await ctx.SsTareoDetalleCasa.Where(d => d.TareoId == id).ToListAsync();
            ctx.SsTareoDetalleCasa.RemoveRange(oldCasa);

            var oldContratista = await ctx.SsTareoDetalleContratista.Where(d => d.TareoId == id).ToListAsync();
            ctx.SsTareoDetalleContratista.RemoveRange(oldContratista);

            InsertDetalles(ctx, id, dto);
            await ctx.SaveChangesAsync();

            var (casa, contratista) = await LoadDetallesAsync(ctx, id);
            return MapTareoDto(tareo, casa, contratista);
        }

        // ─── helpers ───────────────────────────────────────────────────────────

        // Réplica del criterio de aplicabilidad de ítem usado en HabTrabajadorRepository
        // (EstadoCalc / GetEntregablesWorkerAsync) — debe evaluarse igual acá para que un
        // trabajador no salga "No Autorizado" en Control de Acceso por un ítem que las demás
        // pantallas de Habilitación ya no le muestran ni cuentan.
        private static bool EsItemAplicable(
            Dictionary<int, SsItemTrabajador> itemsFullMap, int itemId, string workerType,
            string? categoriaWorker, string? obraOficinaNombre, bool esContratista)
        {
            if (!itemsFullMap.TryGetValue(itemId, out var item) || !item.Activo)
                return false;

            return (item.AplicaA == "TODOS" || item.AplicaA == workerType) &&
                   CsvContiene(item.AplicaCategoria, categoriaWorker) &&
                   CsvContiene(item.AplicaObraOficina, obraOficinaNombre) &&
                   !CsvExcluye(item.ExcluyeObraOficina, obraOficinaNombre) &&
                   (!esContratista || !CsvExcluye(item.ExcluyeCategoriaContratista, categoriaWorker));
        }

        private static bool CsvContiene(string? csv, string? valor)
            => csv == null || csv.Split(',', StringSplitOptions.TrimEntries)
                   .Contains(valor ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        private static bool CsvExcluye(string? csv, string? valor)
            => csv != null && csv.Split(',', StringSplitOptions.TrimEntries)
                   .Contains(valor ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        private static void InsertDetalles(AppDbContext ctx, int tareoId, TareoCreateDto dto)
        {
            if (dto.DetallesCasa.Count > 0)
                ctx.SsTareoDetalleCasa.AddRange(dto.DetallesCasa.Select(d => new SsTareoDetalleCasa
                {
                    TareoId = tareoId,
                    PartidaId = d.PartidaId,
                    CantidadPersonas = d.CantidadPersonas
                }));

            if (dto.DetallesContratista.Count > 0)
                ctx.SsTareoDetalleContratista.AddRange(dto.DetallesContratista.Select(d => new SsTareoDetalleContratista
                {
                    TareoId = tareoId,
                    EmpresaId = d.EmpresaId,
                    CantidadPersonas = d.CantidadPersonas
                }));
        }

        private static async Task<(List<TareoDetalleCasaDto> casa, List<TareoDetalleContratistaDto> contratista)>
            LoadDetallesAsync(AppDbContext ctx, int tareoId)
        {
            var casa = await ctx.SsTareoDetalleCasa
                .Where(d => d.TareoId == tareoId)
                .Join(ctx.SsTareoPartida, d => d.PartidaId, p => p.Id, (d, p) => new TareoDetalleCasaDto
                {
                    PartidaId = d.PartidaId,
                    PartidaNombre = p.Nombre,
                    CantidadPersonas = d.CantidadPersonas
                })
                .ToListAsync();

            var contratista = await ctx.SsTareoDetalleContratista
                .Where(d => d.TareoId == tareoId)
                .Join(ctx.Contributor, d => d.EmpresaId, c => c.ContributorId, (d, c) => new TareoDetalleContratistaDto
                {
                    EmpresaId = d.EmpresaId,
                    EmpresaNombre = c.ContributorName,
                    CantidadPersonas = d.CantidadPersonas
                })
                .ToListAsync();

            return (casa, contratista);
        }

        private static TareoDto MapTareoDto(
            SsTareo t,
            List<TareoDetalleCasaDto>? detallesCasa = null,
            List<TareoDetalleContratistaDto>? detallesContratista = null) => new()
        {
            Id = t.Id,
            ProyectoId = t.ProyectoId,
            ProyectoNombre = t.Proyecto?.ProjectDescription ?? "",
            Fecha = t.Fecha,
            Observaciones = t.Observaciones,
            CreadoPor = t.CreadoPor,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            DetallesCasa = detallesCasa ?? [],
            DetallesContratista = detallesContratista ?? []
        };

        private async Task<List<ControlAccesoWorkerDto>> BuildDtosAsync(
            AppDbContext ctx, List<Worker> workers, bool esOficinaCentral = false, int? proyectoId = null)
        {
            if (workers.Count == 0) return [];

            var workerIds = workers.Select(w => w.Id).ToList();

            // Cargar DNIs restringidos activos para marcar el flag en el resultado
            var dnisList = workers
                .Where(w => w.Person?.DocumentIdentityCode != null)
                .Select(w => w.Person!.DocumentIdentityCode!)
                .Distinct()
                .ToList();
            var restringidosDnis = await ctx.SsTrabajadorRestringido
                .Where(r => r.Activo && r.Dni != null && dnisList.Contains(r.Dni))
                .Select(r => r.Dni!)
                .ToHashSetAsync();

            var allVincs = await ctx.WorkerVinculacion
                .Where(v => workerIds.Contains(v.WorkerId) && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id)
                .ToListAsync();

            // Si hay filtro de proyecto, mostrar la vinculación de ESE proyecto cuando exista
            // (el trabajador puede tener otra vinculación más reciente en otro proyecto vía
            // WorkerProyecto/multi-proyecto Casa, y no debe mostrarse ese otro proyecto).
            var vincByWorker = allVincs
                .GroupBy(v => v.WorkerId)
                .ToDictionary(
                    g => g.Key,
                    g => proyectoId.HasValue
                        ? (g.FirstOrDefault(v => v.ProyectoId == proyectoId.Value) ?? g.First())
                        : g.First());

            var empresaIds = vincByWorker.Values
                .Where(v => v.EmpresaId.HasValue)
                .Select(v => v.EmpresaId!.Value)
                .Distinct().ToList();

            var contribMap = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId);

            var proyIds = vincByWorker.Values
                .Where(v => v.ProyectoId.HasValue)
                .Select(v => v.ProyectoId!.Value)
                .Distinct().ToList();

            // Habilitación SSOMA de la empresa (solo aplica a Contratistas — ver esCasa más abajo
            // y esOficinaCentral arriba). Fail-open: una empresa sin filas SSOMA para el proyecto
            // (nunca activada en ese cómputo) simplemente no aparece en el diccionario y se trata
            // como habilitada, para no bloquear a nadie por falta de datos.
            var empresaHabilitadaMap = new Dictionary<(int EmpresaId, int ProyectoId), bool>();
            if (!esOficinaCentral && empresaIds.Count > 0 && proyIds.Count > 0)
            {
                var itemsSsomaIds = await ctx.SsItemEmpresa
                    .Where(i => i.Activo && i.Responsable == "SSOMA" && !EmpresaHabilitacionHelper.ItemsSctrVidaLey.Contains(i.Id))
                    .Select(i => i.Id)
                    .ToHashSetAsync();

                var habEmpresaRows = await ctx.SsHabEmpresa
                    .Where(h => empresaIds.Contains(h.EmpresaId) && proyIds.Contains(h.ProyectoId) && itemsSsomaIds.Contains(h.ItemId))
                    .ToListAsync();

                empresaHabilitadaMap = EmpresaHabilitacionHelper.CalcularHabilitadas(habEmpresaRows, itemsSsomaIds);
            }

            var proyMap = await ctx.Project
                .Where(p => proyIds.Contains(p.ProjectId))
                .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription);

            var itemsFullMap = await ctx.SsItemTrabajador
                .ToDictionaryAsync(i => i.Id);
            var itemCatalog = itemsFullMap.ToDictionary(kv => kv.Key, kv => kv.Value.Nombre);

            var habItems = await ctx.SsHabTrabajador
                .Where(h => workerIds.Contains(h.WorkerId))
                .ToListAsync();

            var habByWorker = habItems
                .GroupBy(h => h.WorkerId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var ahora = DateTime.UtcNow;
            var en7dias = ahora.AddDays(7);
            // Día calendario (no instante) para los chequeos de "ya venció" — ver mismo fix en
            // GetNoAutorizadosAsync más arriba: con "<= ahora" un documento vigente "hasta hoy"
            // ya salía vencido desde la noche anterior en hora Lima.
            var hoy = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            const int sctrItemId = ItemSctr;
            const string itemEmoNombre = "Certificado de Aptitud (EMO)";

            var casaIds = workers
                .Where(w => string.Equals(w.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase))
                .Select(w => w.Id)
                .ToHashSet();

            // Para trabajadores "Casa" el EMO se gestiona vía módulo SSOMA (WorkerEmo), no vía
            // SsHabTrabajador. Estos ids se excluyen del cómputo genérico más abajo para evitar
            // que el registro crudo (normalmente "Falta", nunca aprobado manualmente) se cuente
            // como pendiente duplicado junto al estado real calculado desde WorkerEmo.
            var itemsEmoIds = itemCatalog
                .Where(kv => kv.Value.Contains("EMO", StringComparison.OrdinalIgnoreCase) && kv.Key != HabItemIds.LecturaEmo)
                .Select(kv => kv.Key)
                .ToHashSet();

            Dictionary<int, WorkerEmo> emoByWorker = [];
            if (!esOficinaCentral && casaIds.Count > 0)
            {
                var emos = await ctx.WorkerEmo
                    .Where(e => casaIds.Contains(e.WorkerId) && e.Activo)
                    .ToListAsync();
                emoByWorker = emos
                    .GroupBy(e => e.WorkerId)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.FechaEmo).ThenByDescending(e => e.Id).First());
            }

            return workers.Select(w =>
            {
                vincByWorker.TryGetValue(w.Id, out var vinc);

                var empresaNombre = "";
                var empresaActiva = false;
                var proyectoNombre = "";

                if (vinc?.EmpresaId is int eid && contribMap.TryGetValue(eid, out var contrib))
                {
                    empresaNombre = contrib.ContributorName;
                    empresaActiva = contrib.Active;
                }
                if (vinc?.ProyectoId is int pid && proyMap.TryGetValue(pid, out var pNombre))
                    proyectoNombre = pNombre ?? "";

                habByWorker.TryGetValue(w.Id, out var items);
                items ??= [];

                bool hasPendientes;
                List<string> faltantes;
                List<string> porVencer;
                List<EntregableResumenDto> entregables = [];

                if (esOficinaCentral)
                {
                    var sctr = items.FirstOrDefault(h => h.ItemId == sctrItemId);
                    var sctrOk = sctr != null &&
                                 string.Equals(sctr.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase) &&
                                 sctr.Vigencia.HasValue && sctr.Vigencia.Value >= hoy;
                    hasPendientes = !sctrOk;
                    faltantes = sctrOk ? [] : ["SCTR"];
                    porVencer = [];
                }
                else
                {
                    var esCasa = casaIds.Contains(w.Id);
                    var esContratistaWorker = string.Equals(w.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);
                    var workerType = esCasa ? "CASA" : "CONTRATISTA";
                    var categoriaWorker = w.PuestoCatalogo?.Categoria?.Nombre;
                    var obraOficinaNombre = ObraOficinaStaffIds.Nombre(w.ObraOficinaStaffId);

                    // Para trabajadores Casa, el/los ítem(s) EMO se excluyen del cómputo genérico:
                    // su estado real se calcula más abajo desde WorkerEmo, no desde este registro crudo.
                    // Además se descarta cualquier ítem que ya NO le aplique al trabajador según el
                    // catálogo (categoría/obra-oficina) — mismo criterio que usan la lista de
                    // Trabajadores (EstadoCalc) y el detalle de entregables (GetEntregablesWorkerAsync)
                    // en HabTrabajadorRepository. Sin este filtro, una fila SsHabTrabajador "Falta"
                    // que quedó de una categoría/obra anterior del trabajador (y que esas otras
                    // pantallas ya no muestran ni cuentan) igual bloqueaba acá en Control de Acceso,
                    // mostrando "No Autorizado" para alguien "Habilitado" en Gestión de Ingresos.
                    var itemsGenerico = items
                        .Where(h => !esCasa || !itemsEmoIds.Contains(h.ItemId))
                        .Where(h => EsItemAplicable(itemsFullMap, h.ItemId, workerType, categoriaWorker, obraOficinaNombre, esContratistaWorker))
                        .ToList();

                    hasPendientes = itemsGenerico.Any(h =>
                        h.ItemId != HabItemIds.LecturaEmo &&
                        (h.Estado == "Falta" || h.Estado == "Rechazado" || h.Estado == "Vencido" || h.Estado == "Enviado" ||
                        (h.Estado == "Aprobado" && (!h.Vigencia.HasValue || h.Vigencia.Value < hoy)) ||
                        (h.Estado == "Renovando" && (!h.Vigencia.HasValue || h.Vigencia.Value < hoy))));

                    faltantes = itemsGenerico
                        .Where(h => h.ItemId != HabItemIds.LecturaEmo &&
                                    (h.Estado == "Falta" || h.Estado == "Rechazado" || h.Estado == "Enviado" ||
                                    (h.Estado == "Aprobado" && (!h.Vigencia.HasValue || h.Vigencia.Value < hoy)) ||
                                    (h.Estado == "Renovando" && (!h.Vigencia.HasValue || h.Vigencia.Value < hoy))))
                        .Select(h => itemCatalog.TryGetValue(h.ItemId, out var n) ? n : null)
                        .Where(n => n != null).Select(n => n!)
                        .ToList();

                    porVencer = itemsGenerico
                        .Where(h => h.Estado == "Aprobado" && h.Vigencia.HasValue
                                    && h.Vigencia.Value > ahora && h.Vigencia.Value <= en7dias)
                        .Select(h => itemCatalog.TryGetValue(h.ItemId, out var n) ? n : null)
                        .Where(n => n != null).Select(n => n!)
                        .ToList();

                    entregables = itemsGenerico
                        .Select(h => new EntregableResumenDto
                        {
                            Nombre = itemCatalog.TryGetValue(h.ItemId, out var n) ? n : "",
                            Estado = h.Estado,
                            Vigencia = h.Vigencia
                        })
                        .Where(e => e.Nombre != "")
                        .ToList();

                    if (esCasa)
                    {
                        emoByWorker.TryGetValue(w.Id, out var emo);

                        string emoEstado;
                        DateTime? emoVigencia = null;

                        var emoVigente = emo != null
                            && (emo.Estado == "Vigente" || emo.Estado == "Convalidado")
                            && !(emo.RequiereInterconsulta == true && emo.InterconsultaResuelta == false);

                        if (emoVigente)
                        {
                            emoEstado = "Aprobado";
                            var fechaVenc = emo!.FechaVencimientoCalculada ?? emo.FechaVencimiento;
                            if (fechaVenc.HasValue)
                                emoVigencia = fechaVenc.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                        }
                        else
                        {
                            emoEstado = "Falta";
                        }

                        entregables.Add(new EntregableResumenDto
                        {
                            Nombre = itemEmoNombre,
                            Estado = emoEstado,
                            Vigencia = emoVigencia
                        });

                        var emoEsFaltante = emoEstado == "Falta" ||
                                            (emoEstado == "Aprobado" && emoVigencia.HasValue && emoVigencia.Value < hoy);
                        if (emoEsFaltante)
                        {
                            hasPendientes = true;
                            faltantes.Add(itemEmoNombre);
                        }
                        if (emoEstado == "Aprobado" && emoVigencia.HasValue && emoVigencia.Value > ahora && emoVigencia.Value <= en7dias)
                            porVencer.Add(itemEmoNombre);
                    }
                }

                // La habilitación de empresa solo aplica a Contratistas (nunca a Casa/personal
                // Abril ni a la vista de oficina central) — ver empresaHabilitadaMap arriba.
                var esCasaWorker = casaIds.Contains(w.Id);
                bool empresaHabilitada = true;
                if (!esOficinaCentral && !esCasaWorker && vinc?.EmpresaId is int eidHab && vinc?.ProyectoId is int pidHab)
                    empresaHabilitada = !empresaHabilitadaMap.TryGetValue((eidHab, pidHab), out var habOk) || habOk;

                var dni = w.Person?.DocumentIdentityCode ?? "";
                return new ControlAccesoWorkerDto
                {
                    WorkerId = w.Id,
                    ApellidoNombre = w.Person?.FullName ?? "",
                    Dni = dni,
                    EmpresaNombre = empresaNombre,
                    ProyectoNombre = proyectoNombre,
                    EstadoHabilitacion = (hasPendientes || !empresaHabilitada) ? "No Autorizado" : "Habilitado",
                    EmpresaActiva = empresaActiva,
                    EmpresaHabilitada = empresaHabilitada,
                    MotivoNoAutorizado = !empresaHabilitada ? "Empresa no habilitada (SSOMA)" : null,
                    DocumentosFaltantes = faltantes,
                    DocumentosPorVencer = porVencer,
                    Entregables = entregables,
                    Restringido = !string.IsNullOrEmpty(dni) && restringidosDnis.Contains(dni),
                };
            }).ToList();
        }
    }
}
