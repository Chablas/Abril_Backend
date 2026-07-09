using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Helpers;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Shared.Models;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class HabTrabajadorRepository : IHabTrabajadorRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmailService _emailService;
        private readonly ITrabajadorRestringidoService _restringidoService;
        private readonly ILogger<HabTrabajadorRepository> _logger;

        private const string MensajeRestriccion =
            "No se puede ingresar o reingresar al trabajador. Comuníquese con el área de Administración o SSOMA.";

        private const int ItemRisst = 6;
        private const int ItemRegistroEpp = 5;
        private const int ItemDifusionPts = 10;
        private const int ItemEntregaRecomendaciones = 8;
        private const int ItemTRegistro = 7;

        private const string CategoriaPracticante = "Practicante";

        private const string EmailMedico = "medicinaocupacionalnm@abril.pe";
        private const string EmailGth = "gth@abril.pe";
        private const string EmailAsistentaSocial = "pquispe@abril.pe";

        public HabTrabajadorRepository(
            IDbContextFactory<AppDbContext> factory,
            IEmailService emailService,
            ITrabajadorRestringidoService restringidoService,
            ILogger<HabTrabajadorRepository> logger)
        {
            _factory = factory;
            _emailService = emailService;
            _restringidoService = restringidoService;
            _logger = logger;
        }

        public async Task<(List<WorkerHabilitacionListDto> Items, int Total)> GetWorkersHabilitacionAsync(
            string? search, int? empresaId, int? proyectoId,
            string? estadoHabilitacion, string? contratistaCasa,
            int page, int pageSize, bool soloRetirados = false, bool soloSinEmo = false, bool soloEmoVencido = false, bool soloSinVidaLey = false)
        {
            using var ctx = _factory.CreateDbContext();

            var itemsEmoIds = await ctx.SsItemTrabajador
                .Where(i => i.Nombre.Contains("EMO"))
                .Select(i => i.Id)
                .ToListAsync();

            var baseQuery = ctx.Worker
                .Select(w => new
                {
                    Worker = w,
                    PersonFullName = w.Person != null ? w.Person.FullName : null,
                    PersonDni = w.Person != null ? w.Person.DocumentIdentityCode : null,
                    // Vinculación activa (FechaFin == null) — usada para vista de activos
                    LatestVincActiva = ctx.WorkerVinculacion
                        .Where(v => v.WorkerId == w.Id && v.FechaFin == null)
                        .OrderByDescending(v => v.CreatedAt)
                        .ThenByDescending(v => v.Id)
                        .FirstOrDefault(),
                    // Última vinculación sin importar FechaFin — usada para vista de retirados
                    LatestVincCualquiera = ctx.WorkerVinculacion
                        .Where(v => v.WorkerId == w.Id)
                        .OrderByDescending(v => v.CreatedAt)
                        .ThenByDescending(v => v.Id)
                        .FirstOrDefault(),
                    EstadoCalc =
                        (ctx.SsHabTrabajador.Any(h => h.WorkerId == w.Id &&
                             h.ItemId != HabItemIds.LecturaEmo &&
                             // Estados que NO habilitan:
                             //  - Falta / Rechazado / Vencido: siempre bloquean.
                             //  - Enviado: primera subida aún sin aprobar por Abril → SIEMPRE bloquea.
                             //  - Renovando: renovación subida cuando el documento seguía aprobado y vigente.
                             //    Bloquea SOLO si la vigencia anterior (conservada en Vigencia) ya venció o
                             //    no existe; mientras siga vigente, el trabajador se mantiene habilitado
                             //    aunque la renovación esté pendiente de aprobación.
                             (h.Estado == "Falta" || h.Estado == "Rechazado" || h.Estado == "Vencido" || h.Estado == "Enviado" ||
                              (h.Estado == "Renovando" && (!h.Vigencia.HasValue || h.Vigencia.Value <= DateTime.UtcNow))) &&
                             !(w.ContrataCasa == "Casa" && itemsEmoIds.Contains(h.ItemId)) &&
                             // El item debe aplicarle de verdad al trabajador. Se compara IGUAL que
                             // el checklist (helper CsvContiene): por token exacto e ignorando
                             // mayúsculas. Se envuelve el CSV y el valor con comas para no hacer
                             // match por substring (","+csv+"," contiene ","+valor+","), y se
                             // normaliza el espacio tras la coma para replicar el TrimEntries.
                             ctx.SsItemTrabajador.Any(i => i.Id == h.ItemId && i.Activo &&
                                 (i.AplicaCategoria == null || ("," + i.AplicaCategoria.Replace(", ", ",") + ",").ToLower().Contains(("," + (w.Categoria ?? "") + ",").ToLower())) &&
                                 (i.AplicaObraOficina == null || ("," + i.AplicaObraOficina.Replace(", ", ",") + ",").ToLower().Contains(("," + (w.ObraOficina ?? "") + ",").ToLower())) &&
                                 (i.ExcluyeObraOficina == null || !("," + i.ExcluyeObraOficina.Replace(", ", ",") + ",").ToLower().Contains(("," + (w.ObraOficina ?? "") + ",").ToLower())) &&
                                 (w.ContrataCasa != "Contratista" || i.ExcluyeCategoriaContratista == null || !("," + i.ExcluyeCategoriaContratista.Replace(", ", ",") + ",").ToLower().Contains(("," + (w.Categoria ?? "") + ",").ToLower()))))
                         || (w.ContrataCasa == "Casa" && !ctx.WorkerEmo.Any(e => e.WorkerId == w.Id &&
                             e.Activo && (e.Estado == "Vigente" || e.Estado == "Convalidado"))))
                        ? "No Autorizado"
                        : ctx.SsHabTrabajador.Any(h => h.WorkerId == w.Id &&
                            h.ItemId != HabItemIds.LecturaEmo &&
                            h.Estado == "En plazo" &&
                            !(w.ContrataCasa == "Casa" && itemsEmoIds.Contains(h.ItemId)))
                        ? "Autorizado Temporalmente"
                        : "Habilitado"
                });

            if (soloRetirados)
                baseQuery = baseQuery.Where(x => x.Worker.Estado == "RETIRADO");
            else
                baseQuery = baseQuery.Where(x => x.Worker.Estado == null || x.Worker.Estado != "RETIRADO");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                baseQuery = baseQuery.Where(x =>
                    (x.PersonFullName != null && x.PersonFullName.ToLower().Contains(s)) ||
                    (x.PersonDni != null && x.PersonDni.Contains(s)));
            }

            if (empresaId.HasValue)
            {
                if (soloRetirados)
                    baseQuery = baseQuery.Where(x => x.LatestVincCualquiera != null && x.LatestVincCualquiera.EmpresaId == empresaId.Value);
                else
                    baseQuery = baseQuery.Where(x => x.LatestVincActiva != null && x.LatestVincActiva.EmpresaId == empresaId.Value);
            }

            if (proyectoId.HasValue)
            {
                if (soloRetirados)
                    baseQuery = baseQuery.Where(x => x.LatestVincCualquiera != null && x.LatestVincCualquiera.ProyectoId == proyectoId.Value);
                else
                    baseQuery = baseQuery.Where(x => x.LatestVincActiva != null && x.LatestVincActiva.ProyectoId == proyectoId.Value);
            }

            var countAntes = await baseQuery.CountAsync();
            _logger.LogInformation("[HAB DEBUG] proyectoId={pId} count={c}", proyectoId, countAntes);

            if (!string.IsNullOrWhiteSpace(contratistaCasa))
            {
                var cc = contratistaCasa.Trim();
                baseQuery = baseQuery.Where(x => x.Worker.ContrataCasa == cc);
            }

            if (!string.IsNullOrWhiteSpace(estadoHabilitacion))
                baseQuery = baseQuery.Where(x => x.EstadoCalc == estadoHabilitacion);

            if (soloSinEmo)
                baseQuery = baseQuery.Where(x =>
                    x.Worker.FechaRetiro == null
                    && ctx.WorkerVinculacion.Any(v => v.WorkerId == x.Worker.Id
                                                   && v.FechaFin == null
                                                   && ctx.Contributor.Any(c => c.ContributorId == v.EmpresaId && c.EsAbril))
                    && !ctx.WorkerEmo.Any(e => e.WorkerId == x.Worker.Id && e.Activo));

            if (soloSinVidaLey)
                baseQuery = baseQuery.Where(x =>
                    x.Worker.FechaRetiro == null
                    && (x.Worker.ObraOficina == "Oficina Central" || x.Worker.ObraOficina == "Staff")
                    && x.Worker.ContrataCasa == "Casa"
                    && x.Worker.Categoria != "Practicante"
                    && ctx.WorkerVinculacion.Any(v => v.WorkerId == x.Worker.Id
                                                   && v.FechaFin == null
                                                   && ctx.Contributor.Any(c => c.ContributorId == v.EmpresaId && c.EsAbril))
                    && !ctx.SsHabTrabajador.Any(h => h.WorkerId == x.Worker.Id
                                                  && h.ItemId == 13
                                                  && h.Estado == "Aprobado"));

            if (soloEmoVencido)
            {
                var hoy = DateOnly.FromDateTime(DateTime.Today);
                baseQuery = baseQuery.Where(x =>
                    ctx.WorkerEmo.Any(e => e.WorkerId == x.Worker.Id
                                       && e.Activo
                                       && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) != null
                                       && (e.FechaVencimientoCalculada ?? e.FechaVencimiento) < hoy));
            }

            var total = await baseQuery.CountAsync();

            var pageRows = await baseQuery
                .OrderBy(x => x.PersonFullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var empresaIds = pageRows
                .Select(r => (soloRetirados ? r.LatestVincCualquiera : r.LatestVincActiva)?.EmpresaId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var proyectoIds = pageRows
                .Select(r => (soloRetirados ? r.LatestVincCualquiera : r.LatestVincActiva)?.ProyectoId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var empresaMap = await ctx.Contributor
                .Where(c => empresaIds.Contains(c.ContributorId))
                .ToDictionaryAsync(c => c.ContributorId, c => c.ContributorName);

            var proyectos = await ctx.Project
                .Where(p => proyectoIds.Contains(p.ProjectId))
                .Select(p => new { p.ProjectId, p.ProjectDescription })
                .ToListAsync();

            var proyectoMap = proyectos.ToDictionary(p => p.ProjectId, p => p.ProjectDescription);

            var workerIds = pageRows.Select(r => r.Worker.Id).ToList();

            var emoMap = await ctx.WorkerEmo
                .Where(e => workerIds.Contains(e.WorkerId) && e.Activo
                         && (e.Estado == "Vigente" || e.Estado == "Convalidado"))
                .GroupBy(e => e.WorkerId)
                .Select(g => new
                {
                    WorkerId = g.Key,
                    FechaVencimiento = g.OrderByDescending(e => e.FechaVencimiento)
                                        .Select(e => e.FechaVencimiento)
                                        .FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.WorkerId, x => x.FechaVencimiento);

            // Trae la programación MÁS RECIENTE de cada trabajador (sin filtrar por estado):
            // si se filtrara "Completado"/"Cancelado"/"Rechazado" antes de ordenar, una
            // programación vieja "No se presentó" quedaría como "la más reciente" para
            // siempre, aunque exista una programación posterior ya completada.
            var progMapRaw = await ctx.SsProgramacionEmo
                .Where(p => workerIds.Contains(p.WorkerId))
                .GroupBy(p => p.WorkerId)
                .Select(g => new
                {
                    WorkerId = g.Key,
                    Estado = g.OrderByDescending(p => p.FechaProgramada)
                               .Select(p => (string?)p.Estado)
                               .FirstOrDefault()
                })
                .ToListAsync();

            // Solo se muestra como badge si la programación más reciente sigue "abierta"
            // (no es un estado terminal ya resuelto).
            var progMap = progMapRaw
                .Where(x => x.Estado != "Completado"
                         && x.Estado != "Cancelado"
                         && x.Estado != "Rechazado por Clínica")
                .ToDictionary(x => x.WorkerId, x => x.Estado);

            var interconsultaWorkerIds = (await ctx.SsInterconsulta
                .Where(i => workerIds.Contains(i.WorkerId) && i.Estado == "Pendiente")
                .Select(i => i.WorkerId)
                .Distinct()
                .ToListAsync()).ToHashSet();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var items = pageRows.Select(r =>
            {
                var vinc = soloRetirados ? r.LatestVincCualquiera : r.LatestVincActiva;
                emoMap.TryGetValue(r.Worker.Id, out var emoVenc);
                progMap.TryGetValue(r.Worker.Id, out var progEstado);
                var estadoProg = progEstado != null
                    ? (interconsultaWorkerIds.Contains(r.Worker.Id) ? "Interconsulta" : progEstado)
                    : null;
                return new WorkerHabilitacionListDto
                {
                    WorkerId = r.Worker.Id,
                    ApellidoNombre = r.PersonFullName ?? string.Empty,
                    Dni = r.PersonDni ?? string.Empty,
                    EmpresaId = vinc?.EmpresaId,
                    EmpresaNombre = vinc?.EmpresaId is int eid && empresaMap.TryGetValue(eid, out var en) ? en : null,
                    ProyectoActualId = vinc?.ProyectoId,
                    ProyectoActual = vinc?.ProyectoId is int pid && proyectoMap.TryGetValue(pid, out var pn) ? pn : null,
                    EstadoHabilitacion = r.EstadoCalc,
                    Categoria = r.Worker.Categoria,
                    Ocupacion = r.Worker.Ocupacion,
                    ContrataCasa = r.Worker.ContrataCasa,
                    ObraOficina = r.Worker.ObraOficina,
                    EstadoWorker = r.Worker.Estado ?? "ACTIVO",
                    TieneEmo = emoMap.ContainsKey(r.Worker.Id),
                    DiasRestantesEmo = emoVenc.HasValue
                        ? (int?)(emoVenc.Value.DayNumber - today.DayNumber)
                        : null,
                    EstadoProgramacionEmo = estadoProg,
                    AniosExperiencia = r.Worker.AniosExperiencia,
                    FechaIngreso = r.Worker.FechaIngreso.HasValue ? r.Worker.FechaIngreso.Value.ToString("yyyy-MM-dd") : null
                };
            }).ToList();

            return (items, total);
        }

        public async Task<int?> GetEntregableItemIdAsync(int habTrabajadorId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsHabTrabajador
                .Where(h => h.Id == habTrabajadorId)
                .Select(h => (int?)h.ItemId)
                .FirstOrDefaultAsync();
        }

        public async Task<List<WorkerEntregableDto>> GetEntregablesWorkerAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var workerType = string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase)
                ? "CASA"
                : "CONTRATISTA";

            var esContratista = string.Equals(worker.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);

            var items = await ctx.SsItemTrabajador
                .Where(i => i.Activo && (i.AplicaA == "TODOS" || i.AplicaA == workerType))
                .OrderBy(i => i.Orden)
                .ToListAsync();

            items = items
                .Where(i => CsvContiene(i.AplicaCategoria, worker.Categoria))
                .Where(i => CsvContiene(i.AplicaObraOficina, worker.ObraOficina))
                .Where(i => !CsvExcluye(i.ExcluyeObraOficina, worker.ObraOficina))
                .Where(i => !esContratista || !CsvExcluye(i.ExcluyeCategoriaContratista, worker.Categoria))
                .ToList();

            var emoItems = items.Where(i => i.Nombre.Contains("EMO", StringComparison.OrdinalIgnoreCase)
                                          && i.Id != HabItemIds.LecturaEmo
                                          && !esContratista).ToList();
            var nonEmoItems = items.Except(emoItems).ToList();
            var nonEmoIds = nonEmoItems.Select(i => i.Id).ToList();

            var existentes = await ctx.SsHabTrabajador
                .Where(h => h.WorkerId == workerId && nonEmoIds.Contains(h.ItemId))
                .ToListAsync();

            var nonEmoMap = nonEmoItems.ToDictionary(i => i.Id);

            var entregables = existentes
                .Where(h => nonEmoMap.ContainsKey(h.ItemId))
                .Select(h =>
                {
                    var item = nonEmoMap[h.ItemId];
                    return new WorkerEntregableDto
                    {
                        Id = h.Id,
                        ItemId = h.ItemId,
                        NombreItem = item.Nombre,
                        Estado = h.Estado,
                        Vigencia = h.Vigencia,
                        VigenciaPropuesta = h.VigenciaPropuesta,
                        ArchivoUrl = h.ArchivoUrl,
                        ObsAbril = h.ObsAbril,
                        ObsContratista = h.ObsContratista,
                        RequiereVigencia = item.RequiereVigencia,
                        EsSctrVidaley = item.EsSctrVidaley,
                        Responsable = item.Responsable
                    };
                })
                .ToList();

            if (emoItems.Count > 0)
            {
                var ultimoEmo = await ctx.WorkerEmo
                    .Where(e => e.WorkerId == workerId && e.Activo)
                    .OrderByDescending(e => e.FechaEmo)
                    .FirstOrDefaultAsync();

                var vigente = ultimoEmo != null
                    && (ultimoEmo.Estado == "Vigente" || ultimoEmo.Estado == "Convalidado")
                    && !(ultimoEmo.RequiereInterconsulta == true && ultimoEmo.InterconsultaResuelta == false);

                DateTime? vigenciaEmo = null;
                if (vigente)
                {
                    var fechaVenc = ultimoEmo!.FechaVencimientoCalculada ?? ultimoEmo.FechaVencimiento;
                    if (fechaVenc.HasValue)
                        vigenciaEmo = fechaVenc.Value.ToDateTime(TimeOnly.MinValue);
                }

                foreach (var item in emoItems)
                {
                    entregables.Add(new WorkerEntregableDto
                    {
                        Id = 0,
                        ItemId = item.Id,
                        NombreItem = item.Nombre,
                        Estado = vigente ? "Aprobado" : "Falta",
                        Vigencia = vigente ? vigenciaEmo : null,
                        ArchivoUrl = null,
                        ObsAbril = "Gestionado por módulo SSOMA",
                        ObsContratista = null,
                        RequiereVigencia = item.RequiereVigencia,
                        EsSctrVidaley = item.EsSctrVidaley,
                        Responsable = item.Responsable
                    });
                }
            }

            var ordenMap = items.ToDictionary(i => i.Id, i => i.Orden);
            return entregables.OrderBy(d => ordenMap[d.ItemId]).ToList();
        }

        public async Task<SsHabTrabajador> UpdateEntregableAsync(int id, WorkerEntregableUpdateDto dto, int? userId, int? empresaId = null)
        {
            using var ctx = _factory.CreateDbContext();

            var entregable = await ctx.SsHabTrabajador
                .Include(h => h.Item)
                .FirstOrDefaultAsync(h => h.Id == id)
                ?? throw new AbrilException("Entregable no encontrado.", 404);

            var estadoAnterior = entregable.Estado;
            var vigenciaAnterior = entregable.Vigencia;

            var esArchivoNuevo = !string.IsNullOrWhiteSpace(dto.ArchivoUrl) && dto.ArchivoUrl != entregable.ArchivoUrl;
            var esAprobacion = string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase);
            var esRechazo = string.Equals(dto.Estado, "Rechazado", StringComparison.OrdinalIgnoreCase);

            if (esArchivoNuevo || esAprobacion || esRechazo)
            {
                var vinculacion = await ctx.WorkerVinculacion
                    .Where(v => v.WorkerId == entregable.WorkerId && v.FechaFin == null)
                    .OrderByDescending(v => v.CreatedAt)
                    .ThenByDescending(v => v.Id)
                    .FirstOrDefaultAsync();

                int? ssEmpresaId = empresaId;

                var versionActual = await ctx.SsHabDocumentoVersion
                    .CountAsync(v => v.HabTrabajadorId == id);

                ctx.SsHabDocumentoVersion.Add(new SsHabDocumentoVersion
                {
                    HabTrabajadorId = id,
                    Version = versionActual + 1,
                    ArchivoUrl = (esArchivoNuevo ? dto.ArchivoUrl : entregable.ArchivoUrl) ?? string.Empty,
                    SubidoPorUserId = userId,
                    SubidoPorEmpresaId = ssEmpresaId,
                    EstadoAlSubir = dto.Estado,
                    EstadoAnterior = estadoAnterior,
                    ProyectoId = vinculacion?.ProyectoId,
                    EmpresaId = vinculacion?.EmpresaId,
                    AprobadoPorUserId = esAprobacion ? userId : null,
                    MotivoRechazo = esRechazo ? dto.ObsAbril : null,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (!string.IsNullOrEmpty(dto.Estado))
                entregable.Estado = dto.Estado;
            if (!string.IsNullOrEmpty(dto.Estado) || dto.Vigencia.HasValue)
            {
                var requiereV = entregable.Item?.RequiereVigencia ?? true;
                // Preservar vigencia existente cuando el estado nuevo es Enviado o Aprobado y no viene fecha
                var preservar = (string.Equals(dto.Estado, "Enviado", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
                    && !dto.Vigencia.HasValue
                    && entregable.Vigencia.HasValue;
                if (!preservar)
                    entregable.Vigencia = HabilitacionDateHelper.ResolverVigencia(requiereV, entregable.Estado, dto.Vigencia);

                // Rechazar si el item requiere vigencia y quedaría en null tras la operación
                if (requiereV
                    && (string.Equals(dto.Estado, "Enviado", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
                    && !entregable.Vigencia.HasValue)
                    throw new AbrilException("Este documento requiere fecha de vigencia.", 400);
            }

            // Cierre de una renovación (el estado previo era "Renovando")
            if (string.Equals(estadoAnterior, "Renovando", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
                {
                    // Se aprueba la renovación: recién ahora se aplica la vigencia propuesta
                    // (si el aprobador no envió una fecha nueva explícita).
                    if (!dto.Vigencia.HasValue && entregable.VigenciaPropuesta.HasValue)
                        entregable.Vigencia = entregable.VigenciaPropuesta;
                    entregable.VigenciaPropuesta = null;
                }
                else if (string.Equals(dto.Estado, "Rechazado", StringComparison.OrdinalIgnoreCase))
                {
                    // Se rechaza la renovación, pero la aprobación anterior seguía vigente:
                    // el trabajador no debe caerse. Se regresa a "Aprobado" conservando la
                    // vigencia anterior y se descarta la propuesta. El motivo queda en ObsAbril.
                    entregable.Estado = "Aprobado";
                    entregable.Vigencia = vigenciaAnterior;
                    entregable.VigenciaPropuesta = null;
                }
            }

            if (dto.ArchivoUrl is not null) entregable.ArchivoUrl = dto.ArchivoUrl;
            if (dto.ObsAbril is not null) entregable.ObsAbril = dto.ObsAbril;
            if (dto.ObsContratista is not null) entregable.ObsContratista = dto.ObsContratista;
            entregable.UpdatedAt = DateTime.UtcNow;

            if (string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
            {
                entregable.AprobadoPor = userId;
                entregable.FechaAprobacion = DateTime.UtcNow;
            }

            if (entregable.ItemId == HabItemIds.InduccionObra)
            {
                if (string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase))
                {
                    var wpRows = await ctx.WorkerProyecto
                        .Where(wp => wp.WorkerId == entregable.WorkerId && wp.FechaFin == null)
                        .ToListAsync();
                    foreach (var wp in wpRows)
                    {
                        wp.InduccionCompletada = true;
                        wp.FechaInduccion ??= DateOnly.FromDateTime(DateTime.UtcNow);
                        wp.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                }
                else if (string.Equals(dto.Estado, "Falta", StringComparison.OrdinalIgnoreCase))
                {
                    var wpRows = await ctx.WorkerProyecto
                        .Where(wp => wp.WorkerId == entregable.WorkerId && wp.FechaFin == null)
                        .ToListAsync();
                    foreach (var wp in wpRows)
                    {
                        wp.InduccionCompletada = false;
                        wp.FechaInduccion = null;
                        wp.UpdatedAt = DateTimeOffset.UtcNow;
                    }
                }
            }

            await ctx.SaveChangesAsync();

            if ((esAprobacion || esRechazo) && (entregable.ItemId == HabItemIds.Sctr || entregable.ItemId == HabItemIds.VidaLey))
            {
                try
                {
                    await SincronizarPolizasSctrVidaLeyAsync(entregable.WorkerId, entregable.ItemId, ctx);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SincronizarPolizas] Error sincronizando póliza workerId={WorkerId} itemId={ItemId}", entregable.WorkerId, entregable.ItemId);
                }
            }

            return entregable;
        }

        public async Task<List<SsHabDocumentoVersionDto>> GetVersionesDocumentoAsync(int habTrabajadorId)
        {
            using var ctx = _factory.CreateDbContext();
            var versiones = await ctx.SsHabDocumentoVersion
                .Where(v => v.HabTrabajadorId == habTrabajadorId)
                .OrderByDescending(v => v.Version)
                .ToListAsync();

            var userIds = versiones
                .Where(v => v.SubidoPorUserId.HasValue)
                .Select(v => v.SubidoPorUserId!.Value)
                .Distinct()
                .ToList();

            var nombresPorUserId = new Dictionary<int, string?>();
            if (userIds.Count > 0)
            {
                var users = await (
                    from u in ctx.User
                    join p in ctx.Person on u.UserId equals p.UserId
                    where userIds.Contains(u.UserId)
                    select new { u.UserId, p.FullName }
                  ).ToListAsync();

                foreach (var x in users)
                    nombresPorUserId[x.UserId] = x.FullName;
            }

            return versiones.Select(v => new SsHabDocumentoVersionDto
            {
                Id = v.Id,
                HabTrabajadorId = v.HabTrabajadorId,
                Version = v.Version,
                ArchivoUrl = v.ArchivoUrl,
                SubidoPorUserId = v.SubidoPorUserId,
                SubidoPorNombre = v.SubidoPorUserId.HasValue && nombresPorUserId.TryGetValue(v.SubidoPorUserId.Value, out var nombre)
                    ? nombre
                    : null,
                SubidoPorEmpresaId = v.SubidoPorEmpresaId,
                EstadoAlSubir = v.EstadoAlSubir,
                EstadoAnterior = v.EstadoAnterior,
                ProyectoId = v.ProyectoId,
                EmpresaId = v.EmpresaId,
                AprobadoPorUserId = v.AprobadoPorUserId,
                MotivoRechazo = v.MotivoRechazo,
                CreatedAt = v.CreatedAt
            }).ToList();
        }

        public async Task<List<WorkerEventoDto>> GetEventosAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var eventos = await ctx.WorkerEvento
                .Where(e => e.WorkerId == workerId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            if (eventos.Count == 0) return [];

            var proyectoIds = eventos
                .SelectMany(e => new[] { e.ProyectoAnteriorId, e.ProyectoNuevoId })
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

            var empresaIds = eventos
                .SelectMany(e => new[] { e.EmpresaAnteriorId, e.EmpresaNuevaId })
                .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

            var proyectoMap = proyectoIds.Count > 0
                ? await ctx.Project
                    .Where(p => proyectoIds.Contains(p.ProjectId))
                    .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription)
                : new Dictionary<int, string>();

            var empresaMap = empresaIds.Count > 0
                ? await ctx.Contributor
                    .Where(c => empresaIds.Contains(c.ContributorId))
                    .ToDictionaryAsync(c => c.ContributorId, c => c.ContributorName)
                : new Dictionary<int, string>();

            return eventos.Select(e => new WorkerEventoDto
            {
                Id = e.Id,
                TipoEvento = e.TipoEvento,
                Descripcion = e.Descripcion,
                ProyectoAnteriorId = e.ProyectoAnteriorId,
                ProyectoAnteriorNombre = e.ProyectoAnteriorId is int paid && proyectoMap.TryGetValue(paid, out var pan) ? pan : null,
                ProyectoNuevoId = e.ProyectoNuevoId,
                ProyectoNuevoNombre = e.ProyectoNuevoId is int pnid && proyectoMap.TryGetValue(pnid, out var pnn) ? pnn : null,
                EmpresaAnteriorId = e.EmpresaAnteriorId,
                EmpresaAnteriorNombre = e.EmpresaAnteriorId is int eaid && empresaMap.TryGetValue(eaid, out var ean) ? ean : null,
                EmpresaNuevaId = e.EmpresaNuevaId,
                EmpresaNuevaNombre = e.EmpresaNuevaId is int enid && empresaMap.TryGetValue(enid, out var enn) ? enn : null,
                Datos = e.Datos,
                UsuarioId = e.UsuarioId,
                CreatedAt = e.CreatedAt
            }).ToList();
        }

        public async Task CambiarObraAsync(int workerId, WorkerCambiarObraDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker
                .Include(w => w.Person)
                .FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            if (await _restringidoService.EstaRestringidoPorDniAsync(worker.Person?.DocumentIdentityCode))
                throw new AbrilException(MensajeRestriccion, 400);

            if (worker.Estado == "INHABILITADO_SSOMA")
                throw new AbrilException("Trabajador inhabilitado por SSOMA. Comuníquese con el Administrador del Proyecto.", 403);

            var fechaCambio = DateOnly.FromDateTime(dto.FechaCambio);
            var now = DateTimeOffset.UtcNow;
            var esContratista = !string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase);

            var activas = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .ToListAsync();

            int? currentProyectoId = activas.Select(a => a.ProyectoId).FirstOrDefault();
            int? currentEmpresaId = activas.Select(a => a.EmpresaId).FirstOrDefault();

            var esCambioProyecto = dto.NuevoProyectoId != currentProyectoId;
            var esCambioEmpresa = dto.NuevaEmpresaId.HasValue
                && dto.NuevaEmpresaId != currentEmpresaId
                && !esContratista;

            if (dto.NuevaEmpresaId.HasValue && esContratista)
                await ValidarExclusividadEmpresaAsync(ctx, workerId, dto.NuevaEmpresaId.Value);

            var itemsToReset = new HashSet<int>();
            var itemsToRestore = new HashSet<int>();
            var pendingEmails = new List<(List<string> To, string Subject, string Body)>();
            Project? proyectoDestino = null;

            if (esCambioProyecto)
            {
                proyectoDestino = await ctx.Project
                    .FirstOrDefaultAsync(p => p.ProjectId == dto.NuevoProyectoId);

                var yaIndujoEnNuevoProyecto = await ctx.WorkerProyecto
                    .AnyAsync(wp => wp.WorkerId == workerId
                        && wp.ProyectoId == dto.NuevoProyectoId
                        && wp.InduccionCompletada);

                if (!yaIndujoEnNuevoProyecto)
                {
                    itemsToReset.Add(HabItemIds.InduccionObra);

                    if (!string.IsNullOrWhiteSpace(proyectoDestino?.EmailCoordSsoma))
                    {
                        pendingEmails.Add((
                            [proyectoDestino.EmailCoordSsoma],
                            $"Cambio de obra — {worker.Person?.FullName}",
                            BuildBodyReingreso(worker, proyectoDestino, "• Inducción Obra")
                        ));
                    }
                }
                else
                {
                    itemsToRestore.Add(HabItemIds.InduccionObra);
                }
            }

            if (esCambioEmpresa)
            {
                itemsToReset.Add(HabItemIds.Sctr);
                itemsToReset.Add(HabItemIds.VidaLey);
                itemsToReset.Add(HabItemIds.CertAptitud);

                if (proyectoDestino == null)
                {
                    var pidParaEmail = (int?)dto.NuevoProyectoId ?? currentProyectoId;
                    if (pidParaEmail.HasValue)
                        proyectoDestino = await ctx.Project
                            .FirstOrDefaultAsync(p => p.ProjectId == pidParaEmail.Value);
                }

                var esOficinaOStaff =
                    string.Equals(worker.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(worker.ObraOficina, "Staff", StringComparison.OrdinalIgnoreCase);

                var emailSctr = esOficinaOStaff ? EmailGth : proyectoDestino?.EmailCoordAdmin;
                if (!string.IsNullOrWhiteSpace(emailSctr))
                    pendingEmails.Add((
                        [emailSctr!],
                        $"Cambio de obra — SCTR — {worker.Person?.FullName}",
                        BuildBodyReingreso(worker, proyectoDestino, "• SCTR")
                    ));

                var emailVidaLey = esOficinaOStaff ? EmailAsistentaSocial : proyectoDestino?.EmailCoordAdmin;
                if (!string.IsNullOrWhiteSpace(emailVidaLey))
                    pendingEmails.Add((
                        [emailVidaLey!],
                        $"Cambio de obra — Vida Ley — {worker.Person?.FullName}",
                        BuildBodyReingreso(worker, proyectoDestino, "• Vida Ley")
                    ));

                pendingEmails.Add((
                    [EmailMedico],
                    $"Cambio de obra — Certificado de Aptitud — {worker.Person?.FullName}",
                    BuildBodyReingreso(worker, proyectoDestino, "• Certificado de Aptitud (Homologación)")
                ));
            }

            foreach (var v in activas)
            {
                v.FechaFin = fechaCambio;
                v.UpdatedAt = now;
            }

            ctx.WorkerVinculacion.Add(new WorkerVinculacion
            {
                WorkerId = workerId,
                EmpresaId = dto.NuevaEmpresaId ?? currentEmpresaId,
                ProyectoId = dto.NuevoProyectoId,
                FechaInicio = fechaCambio,
                CreatedAt = now
            });

            if (esCambioProyecto)
            {
                await SincronizarWorkerProyectoCambioAsync(
                    ctx,
                    workerId,
                    currentProyectoId,
                    dto.NuevoProyectoId,
                    dto.NuevaEmpresaId ?? currentEmpresaId,
                    fechaCambio,
                    now);
            }

            if (itemsToReset.Count > 0)
            {
                var entregables = await ctx.SsHabTrabajador
                    .Where(h => h.WorkerId == workerId && itemsToReset.Contains(h.ItemId))
                    .ToListAsync();

                foreach (var e in entregables)
                {
                    e.Estado = "Falta";
                    e.Vigencia = null;
                    e.VigenciaPropuesta = null;
                    e.ArchivoUrl = null;
                    e.UpdatedAt = DateTime.UtcNow;
                }
            }

            if (itemsToRestore.Count > 0)
            {
                var entregables = await ctx.SsHabTrabajador
                    .Where(h => h.WorkerId == workerId && itemsToRestore.Contains(h.ItemId))
                    .ToListAsync();

                foreach (var e in entregables)
                {
                    e.Estado = "Aprobado";
                    e.UpdatedAt = DateTime.UtcNow;
                }
            }

            var nowUtc = DateTime.UtcNow;

            if (esCambioProyecto)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.CambioObra,
                    Descripcion = $"Cambio de obra registrado. Fecha: {fechaCambio:dd/MM/yyyy}",
                    ProyectoAnteriorId = currentProyectoId,
                    ProyectoNuevoId = dto.NuevoProyectoId,
                    EmpresaAnteriorId = currentEmpresaId,
                    EmpresaNuevaId = dto.NuevaEmpresaId ?? currentEmpresaId,
                    CreatedAt = nowUtc
                });

            if (esCambioEmpresa)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.CambioEmpresa,
                    Descripcion = $"Cambio de razón social. Fecha: {fechaCambio:dd/MM/yyyy}",
                    EmpresaAnteriorId = currentEmpresaId,
                    EmpresaNuevaId = dto.NuevaEmpresaId,
                    CreatedAt = nowUtc
                });

            foreach (var itemId in itemsToReset)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.EntregableReseteado,
                    Datos = itemId.ToString(),
                    CreatedAt = nowUtc
                });

            // Auto-crear convalidación pendiente si el trabajador cambia de empresa y tiene un EMO activo.
            _logger.LogInformation("[Convalidacion] esCambioEmpresa={EsCambioEmpresa} workerId={WorkerId} NuevaEmpresaId={NuevaEmpresaId} currentEmpresaId={CurrentEmpresaId} esContratista={EsContratista}",
                esCambioEmpresa, workerId, dto.NuevaEmpresaId, currentEmpresaId, esContratista);

            if (esCambioEmpresa)
            {
                var ultimoEmo = await ctx.WorkerEmo
                    .Where(e => e.WorkerId == workerId && e.Activo)
                    .OrderByDescending(e => e.FechaEmo)
                    .ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync();

                _logger.LogInformation("[Convalidacion] ultimoEmo={UltimoEmoId}", ultimoEmo?.Id);

                if (ultimoEmo != null)
                {
                    ctx.WorkerEmoConvalidacion.Add(new WorkerEmoConvalidacion
                    {
                        EmoId = ultimoEmo.Id,
                        EmpresaDestinoId = dto.NuevaEmpresaId,
                        FechaConvalidacion = fechaCambio,
                        Resultado = "Pendiente",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });

                    // Marcar CertAptitud como Pendiente (override del "Falta" ya asignado arriba)
                    var habCert = await ctx.SsHabTrabajador
                        .FirstOrDefaultAsync(h => h.WorkerId == workerId && h.ItemId == HabItemIds.CertAptitud);
                    if (habCert != null)
                    {
                        habCert.Estado = "Pendiente";
                        habCert.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        ctx.SsHabTrabajador.Add(new SsHabTrabajador
                        {
                            WorkerId = workerId,
                            ItemId = HabItemIds.CertAptitud,
                            Estado = "Pendiente",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await ctx.SaveChangesAsync();

            foreach (var (to, subject, body) in pendingEmails)
                await EnviarEmailSilenciosoAsync(to, subject, body);
        }

        public async Task ReingresoAsync(int workerId, WorkerReingresoDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker
                .Include(w => w.Person)
                .FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            if (await _restringidoService.EstaRestringidoPorDniAsync(worker.Person?.DocumentIdentityCode))
                throw new AbrilException(MensajeRestriccion, 400);

            if (worker.Estado == "INHABILITADO_SSOMA")
                throw new AbrilException("Trabajador inhabilitado por SSOMA. Comuníquese con el Administrador del Proyecto.", 403);

            await VerificarNoActivoEnOtraEmpresaAsync(ctx, workerId, dto.NuevaEmpresaId);

            var fechaReingreso = dto.FechaReingreso ?? DateOnly.FromDateTime(DateTime.Today);
            var now = DateTimeOffset.UtcNow;
            var esContratista = !string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase);

            worker.Estado = "ACTIVO";
            worker.FechaRetiro = null;
            worker.UpdatedAt = now;

            var vinculActual = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .FirstOrDefaultAsync();

            var currentProyectoId = vinculActual?.ProyectoId;
            var currentEmpresaId = vinculActual?.EmpresaId;

            // Si el trabajador fue retirado correctamente, no habrá vinculación abierta.
            // Recuperamos la última (cerrada) para preservar empresa/proyecto en el reingreso.
            if (vinculActual == null)
            {
                var vinculAnterior = await ctx.WorkerVinculacion
                    .Where(v => v.WorkerId == workerId)
                    .OrderByDescending(v => v.CreatedAt)
                    .ThenByDescending(v => v.Id)
                    .FirstOrDefaultAsync();
                currentProyectoId = vinculAnterior?.ProyectoId;
                currentEmpresaId  = vinculAnterior?.EmpresaId;
            }

            var esCambioProyecto = dto.NuevoProyectoId.HasValue && dto.NuevoProyectoId != currentProyectoId;
            var esCambioEmpresa = dto.NuevaEmpresaId.HasValue && !esContratista;

            var itemsToReset = new HashSet<int>();
            var pendingEmails = new List<(List<string> To, string Subject, string Body)>();

            Project? proyectoDestino = null;

            if (esCambioProyecto)
            {
                proyectoDestino = await ctx.Project
                    .FirstOrDefaultAsync(p => p.ProjectId == dto.NuevoProyectoId!.Value);

                itemsToReset.Add(HabItemIds.InduccionObra);

                if (!string.IsNullOrWhiteSpace(proyectoDestino?.EmailCoordSsoma))
                {
                    pendingEmails.Add((
                        [proyectoDestino.EmailCoordSsoma],
                        $"Reingreso de trabajador — {worker.Person?.FullName}",
                        BuildBodyReingreso(worker, proyectoDestino, "• Inducción Obra")
                    ));
                }
            }

            if (esCambioEmpresa)
            {
                itemsToReset.Add(HabItemIds.Sctr);
                itemsToReset.Add(HabItemIds.VidaLey);
                itemsToReset.Add(HabItemIds.CertAptitud);

                if (proyectoDestino == null)
                {
                    var pidParaEmail = dto.NuevoProyectoId ?? currentProyectoId;
                    if (pidParaEmail.HasValue)
                        proyectoDestino = await ctx.Project
                            .FirstOrDefaultAsync(p => p.ProjectId == pidParaEmail.Value);
                }

                var esOficinaOStaff =
                    string.Equals(worker.ObraOficina, "Oficina Central", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(worker.ObraOficina, "Staff", StringComparison.OrdinalIgnoreCase);

                var emailSctr = esOficinaOStaff ? EmailGth : proyectoDestino?.EmailCoordAdmin;
                if (!string.IsNullOrWhiteSpace(emailSctr))
                    pendingEmails.Add((
                        [emailSctr!],
                        $"Reingreso de trabajador — SCTR — {worker.Person?.FullName}",
                        BuildBodyReingreso(worker, proyectoDestino, "• SCTR")
                    ));

                var emailVidaLey = esOficinaOStaff ? EmailAsistentaSocial : proyectoDestino?.EmailCoordAdmin;
                if (!string.IsNullOrWhiteSpace(emailVidaLey))
                    pendingEmails.Add((
                        [emailVidaLey!],
                        $"Reingreso de trabajador — Vida Ley — {worker.Person?.FullName}",
                        BuildBodyReingreso(worker, proyectoDestino, "• Vida Ley")
                    ));

                pendingEmails.Add((
                    [EmailMedico],
                    $"Reingreso de trabajador — Certificado de Aptitud — {worker.Person?.FullName}",
                    BuildBodyReingreso(worker, proyectoDestino, "• Certificado de Aptitud (Homologación)")
                ));
            }

            // Siempre cerrar la vinculación anterior (si quedó abierta) y crear una nueva.
            // La vinculación fue cerrada al momento del retiro; el reingreso siempre necesita
            // una nueva vinculación activa independientemente de si cambia proyecto o empresa.
            if (vinculActual != null)
            {
                vinculActual.FechaFin = fechaReingreso;
                vinculActual.UpdatedAt = now;
            }

            ctx.WorkerVinculacion.Add(new WorkerVinculacion
            {
                WorkerId = workerId,
                EmpresaId = dto.NuevaEmpresaId ?? currentEmpresaId,
                ProyectoId = dto.NuevoProyectoId ?? currentProyectoId,
                FechaInicio = fechaReingreso,
                CreatedAt = now
            });

            if (esCambioProyecto && !esContratista && dto.NuevoProyectoId.HasValue)
            {
                await SincronizarWorkerProyectoCambioAsync(
                    ctx,
                    workerId,
                    currentProyectoId,
                    dto.NuevoProyectoId.Value,
                    dto.NuevaEmpresaId ?? currentEmpresaId,
                    fechaReingreso,
                    now);
            }

            if (itemsToReset.Count > 0)
            {
                var entregables = await ctx.SsHabTrabajador
                    .Where(h => h.WorkerId == workerId && itemsToReset.Contains(h.ItemId))
                    .ToListAsync();

                foreach (var e in entregables)
                {
                    e.Estado = "Falta";
                    e.Vigencia = null;
                    e.VigenciaPropuesta = null;
                    e.ArchivoUrl = null;
                    e.UpdatedAt = DateTime.UtcNow;
                }
            }

            var nowUtc = DateTime.UtcNow;

            ctx.WorkerEvento.Add(new WorkerEvento
            {
                WorkerId = workerId,
                TipoEvento = WorkerTipoEvento.Reingreso,
                Descripcion = $"Reingreso registrado. Fecha: {fechaReingreso:dd/MM/yyyy}",
                ProyectoAnteriorId = currentProyectoId,
                ProyectoNuevoId = dto.NuevoProyectoId ?? currentProyectoId,
                EmpresaAnteriorId = currentEmpresaId,
                EmpresaNuevaId = dto.NuevaEmpresaId ?? currentEmpresaId,
                CreatedAt = nowUtc
            });

            if (esCambioProyecto)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.CambioObra,
                    Descripcion = "Cambio de proyecto en reingreso.",
                    ProyectoAnteriorId = currentProyectoId,
                    ProyectoNuevoId = dto.NuevoProyectoId,
                    CreatedAt = nowUtc
                });

            if (esCambioEmpresa)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.CambioEmpresa,
                    Descripcion = "Cambio de empresa en reingreso.",
                    EmpresaAnteriorId = currentEmpresaId,
                    EmpresaNuevaId = dto.NuevaEmpresaId,
                    CreatedAt = nowUtc
                });

            foreach (var itemId in itemsToReset)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.EntregableReseteado,
                    Datos = itemId.ToString(),
                    CreatedAt = nowUtc
                });

            await ctx.SaveChangesAsync();

            // Safety check: garantiza que el worker tiene al menos una vinculación activa.
            // Cubre casos de datos corruptos previos o race conditions inesperadas.
            var openCount = await ctx.WorkerVinculacion
                .CountAsync(v => v.WorkerId == workerId && v.FechaFin == null);
            if (openCount == 0)
            {
                var ultimaCerrada = await ctx.WorkerVinculacion
                    .Where(v => v.WorkerId == workerId)
                    .OrderByDescending(v => v.CreatedAt)
                    .ThenByDescending(v => v.Id)
                    .FirstOrDefaultAsync();
                ctx.WorkerVinculacion.Add(new WorkerVinculacion
                {
                    WorkerId    = workerId,
                    EmpresaId   = ultimaCerrada?.EmpresaId,
                    ProyectoId  = ultimaCerrada?.ProyectoId,
                    FechaInicio = fechaReingreso,
                    CreatedAt   = DateTimeOffset.UtcNow,
                });
                await ctx.SaveChangesAsync();
                _logger.LogWarning(
                    "[ReingresoAsync] Safety check activado: worker {WorkerId} quedó sin vinculación activa — reparada (empresa={Empresa}, proyecto={Proyecto}).",
                    workerId, ultimaCerrada?.EmpresaId, ultimaCerrada?.ProyectoId);
            }

            foreach (var (to, subject, body) in pendingEmails)
                await EnviarEmailSilenciosoAsync(to, subject, body);
        }

        private static string BuildBodyReingreso(Worker worker, Project? proyecto, string itemsHtml)
        {
            var proyectoNombre = proyecto?.ProjectDescription ?? "(sin proyecto asignado)";
            return $@"<p>Estimados,</p>
<p>Se notifica el <strong>reingreso del siguiente trabajador</strong>. Los entregables indicados deben ser actualizados:</p>
<table style='border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px;'>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Trabajador</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.Person?.FullName}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>DNI</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.Person?.DocumentIdentityCode}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Modalidad</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.ContrataCasa}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Proyecto</strong></td><td style='border:1px solid #ddd;padding:8px;'>{proyectoNombre}</td></tr>
</table>
<p><strong>Entregables pendientes:</strong><br/>{itemsHtml}</p>";
        }

        private async Task EnviarEmailSilenciosoAsync(List<string> to, string subject, string body)
        {
            try
            {
                await _emailService.SendAsync(to, subject, body, isHtml: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al enviar correo de reingreso a {Destinatarios}", string.Join(", ", to));
            }
        }

        private static async Task SincronizarWorkerProyectoCambioAsync(
            AppDbContext ctx,
            int workerId,
            int? proyectoAnteriorId,
            int proyectoNuevoId,
            int? empresaNuevaId,
            DateOnly fechaCambio,
            DateTimeOffset now)
        {
            if (proyectoAnteriorId.HasValue && proyectoAnteriorId.Value != proyectoNuevoId)
            {
                var activaAnterior = await ctx.WorkerProyecto
                    .Where(wp => wp.WorkerId == workerId && wp.ProyectoId == proyectoAnteriorId.Value && wp.FechaFin == null)
                    .OrderByDescending(wp => wp.CreatedAt)
                    .ThenByDescending(wp => wp.Id)
                    .FirstOrDefaultAsync();

                if (activaAnterior != null)
                {
                    activaAnterior.FechaFin = fechaCambio;
                    activaAnterior.UpdatedAt = now;
                }
            }

            var yaActivoNuevo = await ctx.WorkerProyecto
                .AnyAsync(wp => wp.WorkerId == workerId && wp.ProyectoId == proyectoNuevoId && wp.FechaFin == null);
            if (yaActivoNuevo) return;

            var previaCerrada = await ctx.WorkerProyecto
                .Where(wp => wp.WorkerId == workerId && wp.ProyectoId == proyectoNuevoId && wp.FechaFin != null)
                .OrderByDescending(wp => wp.CreatedAt)
                .ThenByDescending(wp => wp.Id)
                .FirstOrDefaultAsync();

            if (previaCerrada != null)
            {
                previaCerrada.FechaFin = null;
                previaCerrada.UpdatedAt = now;
                return;
            }

            // Si el trabajador ya tiene "Inducción Obra" aprobada globalmente, el nuevo proyecto
            // hereda esa inducción — no debe quedar como pendiente cuando arriba ya dice Aprobado.
            var induccionYaAprobada = await ctx.SsHabTrabajador
                .AnyAsync(h => h.WorkerId == workerId && h.ItemId == HabItemIds.InduccionObra && h.Estado == "Aprobado");

            ctx.WorkerProyecto.Add(new WorkerProyecto
            {
                WorkerId = workerId,
                ProyectoId = proyectoNuevoId,
                EmpresaId = empresaNuevaId,
                FechaInicio = fechaCambio,
                FechaFin = null,
                InduccionCompletada = induccionYaAprobada,
                FechaInduccion = induccionYaAprobada ? DateOnly.FromDateTime(now.UtcDateTime) : null,
                CreatedAt = now,
                UpdatedAt = null
            });
        }

        public async Task<int?> GetEmpresaActivaWorkerAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .Select(v => v.EmpresaId)
                .FirstOrDefaultAsync();
        }

        public async Task InicializarEntregablesAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var workerType = string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase)
                ? "CASA"
                : "CONTRATISTA";

            var todosItems = await ctx.SsItemTrabajador
                .Where(i => i.Activo)
                .ToListAsync();

            var esContratista = string.Equals(worker.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);
            var esCasaPracticante = workerType == "CASA"
                && string.Equals(worker.Categoria?.Trim(), CategoriaPracticante, StringComparison.OrdinalIgnoreCase);

            var itemsAplicables = todosItems
                .Where(i => i.AplicaA == "TODOS" ||
                            (i.AplicaA == "CASA" && workerType == "CASA") ||
                            (i.AplicaA == "CONTRATISTA" && workerType == "CONTRATISTA"))
                .Where(i => CsvContiene(i.AplicaCategoria, worker.Categoria))
                .Where(i => CsvContiene(i.AplicaObraOficina, worker.ObraOficina))
                .Where(i => !CsvExcluye(i.ExcluyeObraOficina, worker.ObraOficina))
                .Where(i => !esContratista || !CsvExcluye(i.ExcluyeCategoriaContratista, worker.Categoria))
                .Where(i => !(esCasaPracticante && i.Id == HabItemIds.VidaLey))
                .ToList();

            var itemIds = itemsAplicables.Select(i => i.Id).ToList();

            var existentesIds = (await ctx.SsHabTrabajador
                .Where(h => h.WorkerId == workerId && itemIds.Contains(h.ItemId))
                .Select(h => h.ItemId)
                .ToListAsync())
                .ToHashSet();

            var nuevos = itemsAplicables
                .Where(i => !existentesIds.Contains(i.Id))
                .Select(i => new SsHabTrabajador
                {
                    WorkerId = workerId,
                    ItemId = i.Id,
                    Estado = "Falta",
                    Vigencia = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            if (nuevos.Count > 0)
            {
                ctx.SsHabTrabajador.AddRange(nuevos);
                await ctx.SaveChangesAsync();
            }
        }

        private static async Task SincronizarPolizasSctrVidaLeyAsync(int workerId, int itemId, AppDbContext ctx)
        {
            var tipo = itemId == HabItemIds.VidaLey ? "VIDA_LEY" : "SCTR";
            int itemIdTipo = tipo == "SCTR" ? 11 : 13;

            var polizas = await ctx.SsSctrVidaley
                .Where(sv => (sv.Estado == "Enviado" || sv.Estado == "Aprobado" || sv.Estado == "En revision")
                          && sv.Tipo == tipo
                          && ctx.SsSctrVidaLeyWorker.Any(svw => svw.SctrVidaLeyId == sv.Id && svw.WorkerId == workerId))
                .ToListAsync();

            foreach (var poliza in polizas)
            {
                int countEnviado = await ctx.SsSctrVidaLeyWorker
                    .Where(svw => svw.SctrVidaLeyId == poliza.Id)
                    .Join(ctx.SsHabTrabajador,
                          svw => svw.WorkerId,
                          ht => ht.WorkerId,
                          (svw, ht) => ht)
                    .CountAsync(ht => ht.ItemId == itemIdTipo && ht.Estado == "Enviado");

                int countEnRevision = await ctx.SsSctrVidaLeyWorker
                    .Where(svw => svw.SctrVidaLeyId == poliza.Id)
                    .Join(ctx.SsHabTrabajador,
                          svw => svw.WorkerId,
                          ht => ht.WorkerId,
                          (svw, ht) => ht)
                    .CountAsync(ht => ht.ItemId == itemIdTipo && ht.Estado == "En revision");

                var nuevoEstado = countEnviado > 0 ? "Enviado"
                                : countEnRevision > 0 ? "En revision"
                                : "Aprobado";
                if (poliza.Estado != nuevoEstado)
                {
                    poliza.Estado = nuevoEstado;
                    poliza.UpdatedAt = DateTime.UtcNow;
                }
            }

            await ctx.SaveChangesAsync();
        }

        private static bool CsvContiene(string? csv, string? valor)
            => csv == null || csv.Split(',', StringSplitOptions.TrimEntries)
                   .Contains(valor ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        private static bool CsvExcluye(string? csv, string? valor)
            => csv != null && csv.Split(',', StringSplitOptions.TrimEntries)
                   .Contains(valor ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        public async Task<WorkerDetalleDto?> GetByIdAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();
            var w = await ctx.Worker
                .Include(x => x.Person)
                .Include(x => x.Contributor)
                .FirstOrDefaultAsync(x => x.Id == workerId);
            return w is null ? null : MapToDetalle(w);
        }

        public async Task<WorkerDetalleDto> UpdateAsync(int workerId, WorkerUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var w = await ctx.Worker
                .Include(x => x.Person)
                .Include(x => x.Contributor)
                .FirstOrDefaultAsync(x => x.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var categoriaAnterior = w.Categoria;
            var obraOficinaAnterior = w.ObraOficina;

            if (dto.ApellidoNombre is not null && w.Person is not null) w.Person.FullName = dto.ApellidoNombre;
            if (dto.Celular is not null && w.Person is not null) w.Person.PhoneNumber = int.TryParse(dto.Celular, out var ph) ? ph : (int?)null;
            if (dto.EmailCorporativo is not null)    w.EmailCorporativo = dto.EmailCorporativo;
            if (dto.FechaNacimiento.HasValue) w.FechaNacimiento = dto.FechaNacimiento;
            if (dto.FechaIngreso.HasValue) w.FechaIngreso = dto.FechaIngreso;
            if (dto.FechaRetiro.HasValue) w.FechaRetiro = dto.FechaRetiro;
            if (dto.Categoria is not null) w.Categoria = dto.Categoria;
            if (dto.Ocupacion is not null) w.Ocupacion = dto.Ocupacion;
            if (dto.OcupacionId.HasValue) w.OcupacionId = dto.OcupacionId;
            if (dto.Area is not null) w.Area = dto.Area;
            if (dto.Subarea is not null) w.Subarea = dto.Subarea;
            if (dto.ContrataCasa is not null) w.ContrataCasa = dto.ContrataCasa;
            if (dto.ObraOficina is not null) w.ObraOficina = dto.ObraOficina;
            // Match interno: deriva el nodo normalizado area_scope a partir del texto capturado.
            w.AreaScopeId = Abril_Backend.Shared.Services.AreaScopeMatcher.Resolve(w.Area, w.Subarea, w.ObraOficina);
            if (dto.Jefatura is not null) w.Jefatura = dto.Jefatura;
            if (dto.Estado is not null) w.Estado = dto.Estado;
            if (dto.HabilitadoObra.HasValue) w.HabilitadoObra = dto.HabilitadoObra;
            if (dto.Sctr.HasValue) w.Sctr = dto.Sctr;
            if (dto.CondicionMedica is not null) w.CondicionMedica = dto.CondicionMedica;
            if (dto.Procedencia is not null) w.Procedencia = dto.Procedencia;
            if (dto.Notas is not null) w.Notas = dto.Notas;
            if (dto.PuntosInfraccion.HasValue) w.PuntosInfraccion = dto.PuntosInfraccion;
            if (dto.AniosExperiencia.HasValue) w.AniosExperiencia = dto.AniosExperiencia;

            w.UpdatedAt = DateTimeOffset.UtcNow;

            var esCasa = string.Equals(w.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase);
            var eraPracticante = string.Equals(categoriaAnterior?.Trim(), CategoriaPracticante, StringComparison.OrdinalIgnoreCase);
            var siguePracticante = string.Equals(w.Categoria?.Trim(), CategoriaPracticante, StringComparison.OrdinalIgnoreCase);
            var transicionFueraDePracticante = dto.Categoria is not null && esCasa && eraPracticante && !siguePracticante;

            var vidaLeyCreada = false;
            if (transicionFueraDePracticante)
            {
                var existeVidaLey = await ctx.SsHabTrabajador
                    .AnyAsync(h => h.WorkerId == workerId && h.ItemId == HabItemIds.VidaLey);

                if (!existeVidaLey)
                {
                    var nowUtc = DateTime.UtcNow;
                    ctx.SsHabTrabajador.Add(new SsHabTrabajador
                    {
                        WorkerId = workerId,
                        ItemId = HabItemIds.VidaLey,
                        Estado = "Falta",
                        Vigencia = null,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    });
                    vidaLeyCreada = true;
                }
            }

            string? cambioObraOficinaDestino = null;
            string? cambioObraOficinaEmail = null;
            if (dto.ObraOficina is not null
                && esCasa
                && !string.Equals(obraOficinaAnterior?.Trim(), w.ObraOficina?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(w.ObraOficina?.Trim(), "Staff", StringComparison.OrdinalIgnoreCase))
                {
                    var proyectoActualId = await ctx.WorkerVinculacion
                        .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                        .OrderByDescending(v => v.CreatedAt)
                        .ThenByDescending(v => v.Id)
                        .Select(v => v.ProyectoId)
                        .FirstOrDefaultAsync();

                    if (proyectoActualId.HasValue)
                    {
                        var proyectoActual = await ctx.Project
                            .FirstOrDefaultAsync(p => p.ProjectId == proyectoActualId.Value);
                        if (!string.IsNullOrWhiteSpace(proyectoActual?.EmailCoordAdmin))
                        {
                            cambioObraOficinaDestino = "Staff";
                            cambioObraOficinaEmail = proyectoActual.EmailCoordAdmin;
                        }
                    }
                }
                else if (string.Equals(w.ObraOficina?.Trim(), "Oficina Central", StringComparison.OrdinalIgnoreCase))
                {
                    cambioObraOficinaDestino = "Oficina Central";
                    cambioObraOficinaEmail = EmailAsistentaSocial;
                }
            }

            await ctx.SaveChangesAsync();

            if (vidaLeyCreada)
            {
                var subject = $"Vida Ley pendiente — Cambio de cargo — {w.Person?.FullName}";
                var body = BuildBodyVidaLeyCambioCargo(w, categoriaAnterior, w.Categoria);
                await EnviarEmailSilenciosoAsync(new List<string> { EmailAsistentaSocial }, subject, body);
            }

            if (cambioObraOficinaDestino is not null && cambioObraOficinaEmail is not null)
            {
                var subject = $"Vida Ley pendiente — Cambio a {cambioObraOficinaDestino} — {w.Person?.FullName}";
                var body = BuildBodyVidaLeyCambioObraOficina(w, obraOficinaAnterior, w.ObraOficina);
                await EnviarEmailSilenciosoAsync(new List<string> { cambioObraOficinaEmail }, subject, body);
            }

            return MapToDetalle(w);
        }

        private static string BuildBodyVidaLeyCambioObraOficina(Worker worker, string? obraOficinaAnterior, string? obraOficinaNueva)
        {
            return $@"<p>Estimados,</p>
<p>Se notifica que el siguiente trabajador <strong>cambió de modalidad de obra/oficina</strong>; corresponde gestionar su <strong>Vida Ley</strong>:</p>
<table style='border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px;'>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Trabajador</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.Person?.FullName}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>DNI</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.Person?.DocumentIdentityCode}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Obra/Oficina anterior</strong></td><td style='border:1px solid #ddd;padding:8px;'>{obraOficinaAnterior}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Obra/Oficina nueva</strong></td><td style='border:1px solid #ddd;padding:8px;'>{obraOficinaNueva}</td></tr>
</table>
<p>Por favor proceder con el registro de la <strong>Vida Ley</strong>.</p>";
        }

        private static string BuildBodyVidaLeyCambioCargo(Worker worker, string? cargoAnterior, string? cargoNuevo)
        {
            return $@"<p>Estimada,</p>
<p>Se notifica que el siguiente trabajador <strong>cambió de cargo</strong>; corresponde gestionar su <strong>Vida Ley</strong>:</p>
<table style='border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px;'>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Trabajador</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.Person?.FullName}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>DNI</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.Person?.DocumentIdentityCode}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Cargo anterior</strong></td><td style='border:1px solid #ddd;padding:8px;'>{cargoAnterior}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Cargo nuevo</strong></td><td style='border:1px solid #ddd;padding:8px;'>{cargoNuevo}</td></tr>
</table>
<p>Por favor proceder con el registro de la <strong>Vida Ley</strong>.</p>";
        }

        private static WorkerDetalleDto MapToDetalle(Worker w) => new()
        {
            Id = w.Id,
            IdTrabajador = w.IdTrabajador,
            ApellidoNombre = w.Person?.FullName,
            Dni = w.Person?.DocumentIdentityCode,
            Ruc = w.Contributor?.ContributorRuc,
            Celular = w.Person?.PhoneNumber?.ToString(),
            EmailCorporativo = w.EmailCorporativo,
            FechaNacimiento = w.FechaNacimiento,
            Sexo = w.Person?.Sexo,
            FechaIngreso = w.FechaIngreso,
            FechaRetiro = w.FechaRetiro,
            Categoria = w.Categoria,
            Ocupacion = w.Ocupacion,
            OcupacionId = w.OcupacionId,
            Puesto = w.Puesto,
            Area = w.Area,
            Subarea = w.Subarea,
            ContrataCasa = w.ContrataCasa,
            ObraOficina = w.ObraOficina,
            Jefatura = w.Jefatura,
            Estado = w.Estado,
            HabilitadoObra = w.HabilitadoObra,
            Sctr = w.Sctr,
            CondicionMedica = w.CondicionMedica,
            Procedencia = w.Procedencia,
            Notas = w.Notas,
            PuntosInfraccion = w.PuntosInfraccion,
            AniosExperiencia = w.AniosExperiencia
        };

        public async Task BajaAsync(int workerId, DateOnly fechaRetiro)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            worker.Estado = "RETIRADO";
            worker.FechaRetiro = fechaRetiro;
            worker.UpdatedAt = DateTimeOffset.UtcNow;

            var vinculacion = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .FirstOrDefaultAsync();

            if (vinculacion != null)
            {
                vinculacion.FechaFin = fechaRetiro;
                vinculacion.UpdatedAt = DateTimeOffset.UtcNow;
            }

            var asignacionesActivas = await ctx.WorkerProyecto
                .Where(wp => wp.WorkerId == workerId && wp.FechaFin == null)
                .ToListAsync();

            var nowOffset = DateTimeOffset.UtcNow;
            foreach (var wp in asignacionesActivas)
            {
                wp.FechaFin = fechaRetiro;
                wp.UpdatedAt = nowOffset;
            }

            ctx.WorkerEvento.Add(new WorkerEvento
            {
                WorkerId = workerId,
                TipoEvento = WorkerTipoEvento.Baja,
                Descripcion = $"Baja registrada. Fecha retiro: {fechaRetiro:dd/MM/yyyy}",
                ProyectoAnteriorId = vinculacion?.ProyectoId,
                EmpresaAnteriorId = vinculacion?.EmpresaId,
                CreatedAt = DateTime.UtcNow
            });

            if (vinculacion?.EmpresaId != null)
            {
                var usuariosContratista = await ctx.SsContratistaUsuarios
                    .Where(u => u.WorkerId == workerId
                             && u.ContractorId == vinculacion.EmpresaId.Value
                             && u.Activo)
                    .ToListAsync();
                foreach (var u in usuariosContratista)
                    u.Activo = false;
            }

            await ctx.SaveChangesAsync();
        }

        public async Task BajaMasivaAsync(List<int> ids, DateOnly fechaRetiro)
        {
            if (ids is null || ids.Count == 0) return;

            using var ctx = _factory.CreateDbContext();

            var workers = await ctx.Worker
                .Where(w => ids.Contains(w.Id))
                .ToListAsync();

            if (workers.Count == 0) return;

            var now = DateTimeOffset.UtcNow;
            foreach (var w in workers)
            {
                w.Estado = "RETIRADO";
                w.FechaRetiro = fechaRetiro;
                w.UpdatedAt = now;
            }

            var workerIds = workers.Select(w => w.Id).ToList();
            var vinculaciones = await ctx.WorkerVinculacion
                .Where(v => workerIds.Contains(v.WorkerId) && v.FechaFin == null)
                .ToListAsync();

            foreach (var v in vinculaciones)
            {
                v.FechaFin = fechaRetiro;
                v.UpdatedAt = now;
            }

            var asignacionesActivas = await ctx.WorkerProyecto
                .Where(wp => workerIds.Contains(wp.WorkerId) && wp.FechaFin == null)
                .ToListAsync();

            foreach (var wp in asignacionesActivas)
            {
                wp.FechaFin = fechaRetiro;
                wp.UpdatedAt = now;
            }

            var vincMap = vinculaciones
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First());

            var usuariosContratista = await ctx.SsContratistaUsuarios
                .Where(u => u.WorkerId != null && workerIds.Contains(u.WorkerId.Value) && u.Activo)
                .ToListAsync();

            foreach (var w in workers)
            {
                vincMap.TryGetValue(w.Id, out var vinc);
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = w.Id,
                    TipoEvento = WorkerTipoEvento.Baja,
                    Descripcion = $"Baja masiva. Fecha retiro: {fechaRetiro:dd/MM/yyyy}",
                    ProyectoAnteriorId = vinc?.ProyectoId,
                    EmpresaAnteriorId = vinc?.EmpresaId,
                    CreatedAt = DateTime.UtcNow
                });

                if (vinc?.EmpresaId != null)
                {
                    foreach (var u in usuariosContratista.Where(u => u.WorkerId == w.Id && u.ContractorId == vinc.EmpresaId.Value))
                        u.Activo = false;
                }
            }

            await ctx.SaveChangesAsync();
        }

        private static async Task VerificarNoActivoEnOtraEmpresaAsync(AppDbContext ctx, int workerId, int? empresaIdNueva)
        {
            var vinculActiva = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .Select(v => new { v.EmpresaId })
                .FirstOrDefaultAsync();

            if (vinculActiva != null && vinculActiva.EmpresaId.HasValue && vinculActiva.EmpresaId != empresaIdNueva)
                throw new AbrilException(
                    "El trabajador ya se encuentra activo en otra empresa. Debe ser retirado antes de poder registrarlo en una nueva empresa.",
                    400);
        }

        private static async Task ValidarExclusividadEmpresaAsync(
            AppDbContext ctx, int workerId, int empresaSolicitanteId)
        {
            var activa = await ctx.WorkerVinculacion
                .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .FirstOrDefaultAsync();

            if (activa == null || !activa.EmpresaId.HasValue) return;
            if (activa.EmpresaId.Value == empresaSolicitanteId) return;

            ctx.SsHabBloqueoLog.Add(new SsHabBloqueoLog
            {
                WorkerId = workerId,
                EmpresaSolicitanteId = empresaSolicitanteId,
                EmpresaPropietariaId = activa.EmpresaId.Value,
                Motivo = "Trabajador con vinculación activa en otra empresa.",
                CreatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();

            throw new AbrilException(
                "Este trabajador está activo en otra empresa y no puede ser habilitado.",
                409);
        }

        public async Task<WorkerProyectoDto> AgregarProyectoAsync(int workerId, AgregarProyectoDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker
                .Include(w => w.Person)
                .FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            if (await _restringidoService.EstaRestringidoPorDniAsync(worker.Person?.DocumentIdentityCode))
                throw new AbrilException(MensajeRestriccion, 400);

            if (worker.Estado == "INHABILITADO_SSOMA")
                throw new AbrilException("Trabajador inhabilitado por SSOMA. Comuníquese con el Administrador del Proyecto.", 403);

            bool esContratista = !string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase);

            if (esContratista)
            {
                var empresaId = await ctx.WorkerVinculacion
                    .Where(v => v.WorkerId == workerId && v.FechaFin == null)
                    .Select(v => v.EmpresaId)
                    .FirstOrDefaultAsync();

                var tieneEntregables = empresaId.HasValue && await ctx.SsEmpresaProyecto
                    .AnyAsync(ep => ep.EmpresaId == empresaId.Value && ep.ProyectoId == dto.ProyectoId);
                if (!tieneEntregables)
                    throw new AbrilException("La empresa no tiene entregables registrados en este proyecto.", 400);
            }

            var proyecto = await ctx.Project.FirstOrDefaultAsync(p => p.ProjectId == dto.ProyectoId)
                ?? throw new AbrilException("Proyecto no encontrado.", 404);

            var yaActivo = await ctx.WorkerProyecto
                .AnyAsync(wp => wp.WorkerId == workerId && wp.ProyectoId == dto.ProyectoId && wp.FechaFin == null);
            if (yaActivo)
                throw new AbrilException("El trabajador ya tiene una asignación activa en este proyecto.", 409);

            var fechaInicio = dto.FechaInicio ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var now = DateTimeOffset.UtcNow;

            // Si el trabajador ya tiene "Inducción Obra" aprobada globalmente, el nuevo proyecto
            // hereda esa inducción — no debe quedar como pendiente cuando arriba ya dice Aprobado.
            var induccionYaAprobada = await ctx.SsHabTrabajador
                .AnyAsync(h => h.WorkerId == workerId && h.ItemId == HabItemIds.InduccionObra && h.Estado == "Aprobado");

            var asignacion = new WorkerProyecto
            {
                WorkerId = workerId,
                ProyectoId = dto.ProyectoId,
                EmpresaId = dto.EmpresaId,
                FechaInicio = fechaInicio,
                FechaFin = null,
                InduccionCompletada = induccionYaAprobada,
                FechaInduccion = induccionYaAprobada ? DateOnly.FromDateTime(DateTime.UtcNow) : null,
                CreatedAt = now,
                UpdatedAt = null
            };

            ctx.WorkerProyecto.Add(asignacion);
            await ctx.SaveChangesAsync();

            string? empresaNombre = null;
            if (asignacion.EmpresaId.HasValue)
                empresaNombre = await ctx.Contributor
                    .Where(c => c.ContributorId == asignacion.EmpresaId.Value)
                    .Select(c => c.ContributorName)
                    .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(proyecto.EmailCoordSsoma))
            {
                var subject = $"Nuevo proyecto asignado — {worker.Person?.FullName}";
                var body = BuildBodyNuevoProyecto(worker, proyecto, fechaInicio);
                await EnviarEmailSilenciosoAsync(new List<string> { proyecto.EmailCoordSsoma }, subject, body);
            }

            return new WorkerProyectoDto
            {
                Id = asignacion.Id,
                WorkerId = asignacion.WorkerId,
                ProyectoId = asignacion.ProyectoId,
                ProyectoNombre = proyecto.ProjectDescription,
                EmpresaId = asignacion.EmpresaId,
                EmpresaNombre = empresaNombre,
                FechaInicio = asignacion.FechaInicio,
                FechaFin = asignacion.FechaFin,
                InduccionCompletada = asignacion.InduccionCompletada,
                FechaInduccion = asignacion.FechaInduccion,
                Activo = true
            };
        }

        public async Task<List<WorkerProyectoDto>> GetProyectosAsync(int workerId)
        {
            using var ctx = _factory.CreateDbContext();

            var workerExiste = await ctx.Worker.AnyAsync(w => w.Id == workerId);
            if (!workerExiste)
                throw new AbrilException("Trabajador no encontrado.", 404);

            var asignaciones = await ctx.WorkerProyecto
                .Where(wp => wp.WorkerId == workerId)
                .ToListAsync();

            if (asignaciones.Count == 0) return new List<WorkerProyectoDto>();

            var proyectoIds = asignaciones.Select(a => a.ProyectoId).Distinct().ToList();
            var proyectoMap = await ctx.Project
                .Where(p => proyectoIds.Contains(p.ProjectId))
                .Select(p => new { p.ProjectId, p.ProjectDescription })
                .ToDictionaryAsync(p => p.ProjectId, p => p.ProjectDescription);

            var empresaIds = asignaciones
                .Where(a => a.EmpresaId.HasValue)
                .Select(a => a.EmpresaId!.Value)
                .Distinct()
                .ToList();
            var empresaMap = empresaIds.Count > 0
                ? await ctx.Contributor
                    .Where(c => empresaIds.Contains(c.ContributorId))
                    .Select(c => new { c.ContributorId, c.ContributorName })
                    .ToDictionaryAsync(c => c.ContributorId, c => c.ContributorName)
                : new Dictionary<int, string>();

            return asignaciones
                .OrderBy(a => a.FechaFin == null ? 0 : 1)
                .ThenByDescending(a => a.FechaInicio)
                .ThenByDescending(a => a.Id)
                .Select(a => new WorkerProyectoDto
                {
                    Id = a.Id,
                    WorkerId = a.WorkerId,
                    ProyectoId = a.ProyectoId,
                    ProyectoNombre = proyectoMap.TryGetValue(a.ProyectoId, out var pn) ? pn : null,
                    EmpresaId = a.EmpresaId,
                    EmpresaNombre = a.EmpresaId.HasValue && empresaMap.TryGetValue(a.EmpresaId.Value, out var en) ? en : null,
                    FechaInicio = a.FechaInicio,
                    FechaFin = a.FechaFin,
                    InduccionCompletada = a.InduccionCompletada,
                    FechaInduccion = a.FechaInduccion,
                    Activo = a.FechaFin == null
                })
                .ToList();
        }

        public async Task RetirarDeProyectoAsync(int workerId, int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var asignacion = await ctx.WorkerProyecto
                .Where(wp => wp.WorkerId == workerId && wp.ProyectoId == proyectoId && wp.FechaFin == null)
                .OrderByDescending(wp => wp.CreatedAt)
                .ThenByDescending(wp => wp.Id)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("No existe una asignación activa para este trabajador en este proyecto.", 404);

            asignacion.FechaFin = DateOnly.FromDateTime(DateTime.UtcNow);
            asignacion.UpdatedAt = DateTimeOffset.UtcNow;

            await ctx.SaveChangesAsync();
        }

        public async Task MarcarInduccionAsync(int workerId, int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();

            var worker = await ctx.Worker.Include(w => w.Person)
                .FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            if (await _restringidoService.EstaRestringidoPorDniAsync(worker.Person?.DocumentIdentityCode))
                throw new AbrilException(MensajeRestriccion, 400);

            var asignacion = await ctx.WorkerProyecto
                .Where(wp => wp.WorkerId == workerId && wp.ProyectoId == proyectoId && wp.FechaFin == null)
                .OrderByDescending(wp => wp.CreatedAt)
                .ThenByDescending(wp => wp.Id)
                .FirstOrDefaultAsync()
                ?? throw new AbrilException("No existe una asignación activa para este trabajador en este proyecto.", 404);

            asignacion.InduccionCompletada = true;
            asignacion.FechaInduccion = DateOnly.FromDateTime(DateTime.UtcNow);
            asignacion.UpdatedAt = DateTimeOffset.UtcNow;

            var now = DateTime.UtcNow;
            var sentinel = HabilitacionDateHelper.ResolverVigencia(false, "Aprobado", null);

            var habInduccion = await ctx.SsHabTrabajador
                .FirstOrDefaultAsync(h => h.WorkerId == workerId && h.ItemId == HabItemIds.InduccionObra);

            if (habInduccion is not null)
            {
                habInduccion.Estado = "Aprobado";
                habInduccion.Vigencia = sentinel;
                habInduccion.UpdatedAt = now;
            }
            else
            {
                ctx.SsHabTrabajador.Add(new SsHabTrabajador
                {
                    WorkerId = workerId,
                    ItemId = HabItemIds.InduccionObra,
                    Estado = "Aprobado",
                    Vigencia = sentinel,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            await ctx.SaveChangesAsync();
        }

        public async Task<List<WorkerReparacionVinculacionDto>> RepararVinculacionesAsync()
        {
            using var ctx = _factory.CreateDbContext();

            // 1. Todos los workers con estado ACTIVO
            var activoIds = await ctx.Worker
                .Where(w => w.Estado == "ACTIVO")
                .Select(w => w.Id)
                .ToListAsync();

            if (activoIds.Count == 0) return [];

            // 2. Subset que YA tiene vinculación abierta
            var conVincActiva = await ctx.WorkerVinculacion
                .Where(v => activoIds.Contains(v.WorkerId) && v.FechaFin == null)
                .Select(v => v.WorkerId)
                .Distinct()
                .ToListAsync();

            var sinVincActiva = activoIds.Except(conVincActiva).ToList();

            if (sinVincActiva.Count == 0) return [];

            // 3. Recuperar en bloque todas las vinculaciones (cerradas) de esos workers
            var todasCerradas = await ctx.WorkerVinculacion
                .Where(v => sinVincActiva.Contains(v.WorkerId))
                .OrderByDescending(v => v.CreatedAt)
                .ThenByDescending(v => v.Id)
                .ToListAsync();

            // La query ya viene ordenada desc; First() da la más reciente por worker
            var ultimaPorWorker = todasCerradas
                .GroupBy(v => v.WorkerId)
                .ToDictionary(g => g.Key, g => g.First());

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var now = DateTimeOffset.UtcNow;
            var reparados = new List<WorkerReparacionVinculacionDto>();

            foreach (var workerId in sinVincActiva)
            {
                ultimaPorWorker.TryGetValue(workerId, out var ultima);
                ctx.WorkerVinculacion.Add(new WorkerVinculacion
                {
                    WorkerId    = workerId,
                    EmpresaId   = ultima?.EmpresaId,
                    ProyectoId  = ultima?.ProyectoId,
                    FechaInicio = hoy,
                    CreatedAt   = now,
                });
                reparados.Add(new WorkerReparacionVinculacionDto
                {
                    WorkerId   = workerId,
                    EmpresaId  = ultima?.EmpresaId,
                    ProyectoId = ultima?.ProyectoId,
                });
                _logger.LogWarning(
                    "[RepararVinculaciones] Worker {WorkerId} reparado (empresa={Empresa}, proyecto={Proyecto}).",
                    workerId, ultima?.EmpresaId, ultima?.ProyectoId);
            }

            await ctx.SaveChangesAsync();
            _logger.LogWarning("[RepararVinculaciones] Total reparados: {Count}.", reparados.Count);
            return reparados;
        }

        public async Task<string?> GetResponsableItemTrabajadorAsync(int entregableId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.SsHabTrabajador
                .Where(h => h.Id == entregableId)
                .Select(h => h.Item != null ? h.Item.Responsable : null)
                .FirstOrDefaultAsync();
        }

        private static string BuildBodyNuevoProyecto(Worker worker, Project proyecto, DateOnly fechaInicio)
        {
            return $@"<p>Estimados,</p>
<p>Se notifica el <strong>nuevo ingreso</strong> del siguiente trabajador al proyecto:</p>
<table style='border-collapse:collapse;font-family:Arial,sans-serif;font-size:14px;'>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Trabajador</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.Person?.FullName}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>DNI</strong></td><td style='border:1px solid #ddd;padding:8px;'>{worker.Person?.DocumentIdentityCode}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Proyecto</strong></td><td style='border:1px solid #ddd;padding:8px;'>{proyecto.ProjectDescription}</td></tr>
  <tr><td style='border:1px solid #ddd;padding:8px;'><strong>Fecha de ingreso</strong></td><td style='border:1px solid #ddd;padding:8px;'>{fechaInicio:dd/MM/yyyy}</td></tr>
</table>
<p>Por favor coordinar la <strong>inducción de obra</strong> y los entregables correspondientes.</p>";
        }
    }
}
