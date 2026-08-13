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
using Abril_Backend.Shared.Helpers;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Repositories
{
    public class HabTrabajadorRepository : IHabTrabajadorRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IEmailService _emailService;
        private readonly ITrabajadorRestringidoService _restringidoService;
        private readonly IJefePersonalizadoService _jefePersonalizado;
        private readonly ILogger<HabTrabajadorRepository> _logger;

        private const string MensajeRestriccion =
            "No se puede ingresar o reingresar al trabajador. Comuníquese con el área de Administración o SSOMA.";

        private const int ItemRisst = 6;
        private const int ItemRegistroEpp = 5;
        private const int ItemDifusionPts = 10;
        private const int ItemEntregaRecomendaciones = 8;
        private const int ItemTRegistro = 7;

        private const string EmailMedico = "medicinaocupacionalnm@abril.pe";
        private const string EmailGth = "gth@abril.pe";
        private const string EmailAsistentaSocial = "pquispe@abril.pe";

        public HabTrabajadorRepository(
            IDbContextFactory<AppDbContext> factory,
            IEmailService emailService,
            ITrabajadorRestringidoService restringidoService,
            IJefePersonalizadoService jefePersonalizado,
            ILogger<HabTrabajadorRepository> logger)
        {
            _factory = factory;
            _emailService = emailService;
            _restringidoService = restringidoService;
            _jefePersonalizado = jefePersonalizado;
            _logger = logger;
        }

        public async Task<(List<WorkerHabilitacionListDto> Items, int Total)> GetWorkersHabilitacionAsync(
            string? search, int? empresaId, int? proyectoId,
            string? estadoHabilitacion, string? contratistaCasa,
            int page, int pageSize, bool soloRetirados = false, bool soloSinEmo = false, bool soloEmoVencido = false, bool soloSinVidaLey = false,
            int? areaScopeId = null)
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
                             // Antes se excluía el ítem EMO (id 4) para "Casa" acá, confiando SOLO
                             // en el chequeo de WorkerEmo.Estado de la línea de abajo como única
                             // fuente de verdad. Si ese estado quedaba desincronizado (p.ej. el job
                             // de vigencias nunca lo marcaba "Vencido" por comparar contra la
                             // columna equivocada — ver VigenciaRevisionService), el trabajador
                             // aparecía "Habilitado" aunque este mismo ítem ya mostrara "Falta"/
                             // "Vencido" en pantalla (casos Díaz Díaz, Algoner, Bolaños). Ahora
                             // cualquiera de las dos fuentes que detecte el problema bloquea.
                             // El ítem 25 (Lectura EMO) sigue excluido globalmente arriba.
                             // El item debe aplicarle de verdad al trabajador. Se compara IGUAL que
                             // el checklist (helper CsvContiene): por token exacto e ignorando
                             // mayúsculas. Se envuelve el CSV y el valor con comas para no hacer
                             // match por substring (","+csv+"," contiene ","+valor+","), y se
                             // normaliza el espacio tras la coma para replicar el TrimEntries.
                             ctx.SsItemTrabajador.Any(i => i.Id == h.ItemId && i.Activo &&
                                 (i.AplicaCategoria == null || ("," + i.AplicaCategoria.Replace(", ", ",") + ",").ToLower().Contains(("," + (w.CategoriaCatalogo == null ? "" : w.CategoriaCatalogo.Nombre) + ",").ToLower())) &&
                                 (i.AplicaObraOficina == null || ("," + i.AplicaObraOficina.Replace(", ", ",") + ",").ToLower().Contains(("," + (w.ObraOficinaStaff == null ? "" : w.ObraOficinaStaff.Name) + ",").ToLower())) &&
                                 (i.ExcluyeObraOficina == null || !("," + i.ExcluyeObraOficina.Replace(", ", ",") + ",").ToLower().Contains(("," + (w.ObraOficinaStaff == null ? "" : w.ObraOficinaStaff.Name) + ",").ToLower())) &&
                                 (w.ContrataCasa != "Contratista" || i.ExcluyeCategoriaContratista == null || !("," + i.ExcluyeCategoriaContratista.Replace(", ", ",") + ",").ToLower().Contains(("," + (w.CategoriaCatalogo == null ? "" : w.CategoriaCatalogo.Nombre) + ",").ToLower()))))
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

            // Búsqueda por palabras en cualquier orden, insensible a mayúsculas y tildes
            // (alineada con app-search-input del front: "perez juan" coincide con "JUAN PÉREZ").
            // Cada palabra debe estar en el nombre o en el documento, así que se acumula un
            // Where por palabra (AND) en vez de un solo Contains sobre la frase completa.
            if (!string.IsNullOrWhiteSpace(search))
            {
                foreach (var word in search.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    var pattern = $"%{word}%";
                    baseQuery = baseQuery.Where(x =>
                        (x.PersonFullName != null &&
                         EF.Functions.ILike(AppDbContext.Unaccent(x.PersonFullName), AppDbContext.Unaccent(pattern)))
                        || (x.PersonDni != null && EF.Functions.ILike(x.PersonDni, pattern)));
                }
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

            if (areaScopeId.HasValue)
            {
                var idsArea = await ctx.ResolveDescendantsAsync(areaScopeId.Value);
                baseQuery = baseQuery.Where(x => x.Worker.AreaScopeId != null && idsArea.Contains(x.Worker.AreaScopeId.Value));
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
                    && (x.Worker.ObraOficinaStaffId == ObraOficinaStaffIds.OficinaCentral
                        || x.Worker.ObraOficinaStaffId == ObraOficinaStaffIds.Staff)
                    && x.Worker.ContrataCasa == "Casa"
                    && (x.Worker.CategoriaId == null || x.Worker.CategoriaId != CategoriaIds.Practicante)
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
                .Where(p => p.State && workerIds.Contains(p.WorkerId))
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

            // Especialidad de la interconsulta pendiente más reciente por trabajador — se usa
            // tanto para el badge "Interconsulta" como para la advertencia del modal
            // "Programar EMO con clínica" (ver ProgramarEmoDialogComponent en el frontend).
            var interconsultaPendienteMap = await ctx.SsInterconsulta
                .Where(i => workerIds.Contains(i.WorkerId) && i.Estado == "Pendiente")
                .GroupBy(i => i.WorkerId)
                .Select(g => new
                {
                    WorkerId = g.Key,
                    Especialidad = g.OrderByDescending(i => i.FechaDerivacion)
                                     .Select(i => i.Especialidad)
                                     .FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.WorkerId, x => x.Especialidad);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var items = pageRows.Select(r =>
            {
                var vinc = soloRetirados ? r.LatestVincCualquiera : r.LatestVincActiva;
                emoMap.TryGetValue(r.Worker.Id, out var emoVenc);
                progMap.TryGetValue(r.Worker.Id, out var progEstado);
                var tieneInterconsultaPendiente = interconsultaPendienteMap.TryGetValue(r.Worker.Id, out var interconsultaEspecialidad);
                var estadoProg = progEstado != null
                    ? (tieneInterconsultaPendiente ? "Interconsulta" : progEstado)
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
                    Categoria = r.Worker.CategoriaCatalogo == null ? null : r.Worker.CategoriaCatalogo.Nombre,
                    Puesto = r.Worker.PuestoCatalogo == null ? null : r.Worker.PuestoCatalogo.Nombre,
                    ContrataCasa = r.Worker.ContrataCasa,
                    ObraOficinaStaffId = r.Worker.ObraOficinaStaffId,
                    ObraOficina = ObraOficinaStaffIds.Nombre(r.Worker.ObraOficinaStaffId),
                    EstadoWorker = r.Worker.Estado ?? "ACTIVO",
                    TieneEmo = emoMap.ContainsKey(r.Worker.Id),
                    DiasRestantesEmo = emoVenc.HasValue
                        ? (int?)(emoVenc.Value.DayNumber - today.DayNumber)
                        : null,
                    EstadoProgramacionEmo = estadoProg,
                    AniosExperiencia = r.Worker.AniosExperiencia,
                    FechaIngreso = r.Worker.FechaIngreso.HasValue ? r.Worker.FechaIngreso.Value.ToString("yyyy-MM-dd") : null,
                    InterconsultaEstado = tieneInterconsultaPendiente ? "Pendiente" : null,
                    InterconsultaEspecialidad = tieneInterconsultaPendiente ? interconsultaEspecialidad : null
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

            var worker = await ctx.Worker
                .Include(w => w.CategoriaCatalogo)
                .FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var esCasa = string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase);
            var workerType = esCasa ? "CASA" : "CONTRATISTA";

            var esContratista = string.Equals(worker.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);
            var categoriaWorker = worker.CategoriaCatalogo?.Nombre;

            var items = await ctx.SsItemTrabajador
                .Where(i => i.Activo && (i.AplicaA == "TODOS" || i.AplicaA == workerType))
                .OrderBy(i => i.Orden)
                .ToListAsync();

            items = items
                .Where(i => CsvContiene(i.AplicaCategoria, categoriaWorker))
                .Where(i => CsvContiene(i.AplicaObraOficina, ObraOficinaStaffIds.Nombre(worker.ObraOficinaStaffId)))
                .Where(i => !CsvExcluye(i.ExcluyeObraOficina, ObraOficinaStaffIds.Nombre(worker.ObraOficinaStaffId)))
                .Where(i => !esContratista || !CsvExcluye(i.ExcluyeCategoriaContratista, categoriaWorker))
                .ToList();

            var emoItems = items.Where(i => i.Nombre.Contains("EMO", StringComparison.OrdinalIgnoreCase)
                                          && i.Id != HabItemIds.LecturaEmo
                                          && esCasa).ToList();
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

                // El ítem CertAptitud es el que Cambiar obra/puesto/razón social pone en
                // "Pendiente" o "Falta" (ver CambiarObraAsync) cuando hay que revisar el EMO —
                // ese estado vive en ss_hab_trabajador, no se puede seguir derivando solo de
                // WorkerEmo.Estado (que nunca pasa por "Pendiente"), o el checklist de acá nunca
                // reflejaría un cambio de obra/puesto/razón social pendiente de convalidar.
                var habCertAptitud = await ctx.SsHabTrabajador
                    .FirstOrDefaultAsync(h => h.WorkerId == workerId && h.ItemId == HabItemIds.CertAptitud);

                foreach (var item in emoItems)
                {
                    if (item.Id == HabItemIds.CertAptitud && habCertAptitud != null)
                    {
                        entregables.Add(new WorkerEntregableDto
                        {
                            Id = habCertAptitud.Id,
                            ItemId = item.Id,
                            NombreItem = item.Nombre,
                            Estado = habCertAptitud.Estado,
                            Vigencia = habCertAptitud.Vigencia,
                            ArchivoUrl = habCertAptitud.ArchivoUrl,
                            ObsAbril = "Gestionado por módulo SSOMA",
                            ObsContratista = null,
                            RequiereVigencia = item.RequiereVigencia,
                            EsSctrVidaley = item.EsSctrVidaley,
                            Responsable = item.Responsable
                        });
                        continue;
                    }

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

            // El ítem 12 es global (una fila por trabajador) y `WorkerProyecto.InduccionCompletada`
            // es su espejo por proyecto, así que hay que mantenerlos sincronizados en ambos
            // sentidos: "Aprobado" marca la inducción, y CUALQUIER otro estado la desmarca.
            // Antes solo se desmarcaba con "Falta", y un ítem 12 pasado a "Rechazado" dejaba el
            // flag en true: el trabajador quedaba "No Autorizado" en la lista pero invisible en
            // "Programar Inducción", que filtra justamente por ese flag (incidencia 2026-08-05).
            //
            // Se evalúa `entregable.Estado` (el valor ya resuelto) y no `dto.Estado`, porque al
            // rechazar una renovación el estado vuelve a "Aprobado" conservando la vigencia
            // anterior: esa inducción sigue siendo válida y no debe borrarse.
            if (entregable.ItemId == HabItemIds.InduccionObra && !string.IsNullOrEmpty(dto.Estado))
            {
                var induccionVigente =
                    string.Equals(entregable.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase);

                var wpRows = await ctx.WorkerProyecto
                    .Where(wp => wp.WorkerId == entregable.WorkerId && wp.FechaFin == null)
                    .ToListAsync();
                foreach (var wp in wpRows)
                {
                    if (induccionVigente)
                    {
                        wp.InduccionCompletada = true;
                        wp.FechaInduccion ??= DateOnly.FromDateTime(DateTime.UtcNow);
                    }
                    else
                    {
                        wp.InduccionCompletada = false;
                        wp.FechaInduccion = null;
                    }
                    wp.UpdatedAt = DateTimeOffset.UtcNow;
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
                .Include(w => w.PuestoCatalogo)
                .FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            if (await _restringidoService.EstaRestringidoPorDniAsync(worker.Person?.DocumentIdentityCode))
                throw new AbrilException(MensajeRestriccion, 400);

            if (worker.Estado == "INHABILITADO_SSOMA")
                throw new AbrilException("Trabajador inhabilitado por SSOMA. Comuníquese con el Administrador del Proyecto.", 403);

            if (worker.Estado == "RETIRADO")
                throw new AbrilException("El trabajador está retirado. Use la opción de Reingreso en vez de Cambiar obra.", 400);

            // No se permite un nuevo cambio de obra/razón social/puesto mientras haya una
            // convalidación sin resolver: encadenar cambios antes de que el médico decida deja
            // convalidaciones "Pendiente" apiladas contra EMOs y empresas destino distintas, sin
            // que quede claro cuál sigue vigente. Primero se resuelve la que está pendiente.
            var tieneConvalidacionPendiente = await ctx.WorkerEmoConvalidacion
                .Join(ctx.WorkerEmo, cv => cv.EmoId, e => e.Id, (cv, e) => new { cv, e })
                .AnyAsync(x => x.e.WorkerId == workerId && x.cv.Resultado == "Pendiente");

            if (tieneConvalidacionPendiente)
                throw new AbrilException(
                    "Este trabajador tiene una convalidación pendiente sin resolver. Debe " +
                    "aprobarla o rechazarla en SSOMA → Salud Ocupacional → Convalidaciones antes " +
                    "de registrar un nuevo cambio de obra, razón social o puesto de trabajo.", 400);

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

            // Un cambio de obra puro (mismo puesto, misma empresa, misma clasificación) no
            // altera nada de la aptitud del trabajador — el EMO sigue siendo válido para el
            // mismo puesto en otra obra. Solo estos tres casos exigen revisar el EMO:
            // El puesto vigente es el del trabajador; worker_vinculaciones.puesto guarda además
            // el nombre congelado en el momento del cambio (snapshot histórico, no un catálogo).
            var currentPuesto = worker.PuestoCatalogo?.Nombre;
            var nuevoPuesto = dto.PuestoId.HasValue
                ? await ctx.Puesto.Where(p => p.PuestoId == dto.PuestoId && p.State)
                    .Select(p => p.Nombre).FirstOrDefaultAsync()
                : null;
            var esCambioPuesto = dto.PuestoId.HasValue && dto.PuestoId != worker.PuestoId;

            var currentObraOficinaStaffId = worker.ObraOficinaStaffId;
            var nuevoObraOficinaStaffId = dto.ObraOficinaStaffId ?? currentObraOficinaStaffId;
            var esCambioRiesgoAscendente = ObraOficinaStaffIds.EsCambioRiesgoCritico(
                currentObraOficinaStaffId, nuevoObraOficinaStaffId);

            if (dto.NuevaEmpresaId.HasValue && esContratista)
                await ValidarExclusividadEmpresaAsync(ctx, workerId, dto.NuevaEmpresaId.Value);

            var itemsToReset = new HashSet<int>();
            var itemsToRestore = new HashSet<int>();
            var pendingEmails = new List<(List<string> To, string Subject, string Body)>();
            Project? proyectoDestino = null;
            // Se hoistea a este scope porque también determina el induccion_completada con el que
            // nace la fila de ss_hab_worker_proyecto del proyecto destino (ver llamada a
            // SincronizarWorkerProyectoCambioAsync más abajo).
            bool yaIndujoEnNuevoProyecto = false;

            if (esCambioProyecto)
            {
                proyectoDestino = await ctx.Project
                    .FirstOrDefaultAsync(p => p.ProjectId == dto.NuevoProyectoId);

                yaIndujoEnNuevoProyecto = await ctx.WorkerProyecto
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
                // SCTR y Vida Ley son pólizas atadas a la razón social — solo el cambio de
                // empresa las afecta, no un simple cambio de puesto u obra.
                itemsToReset.Add(HabItemIds.Sctr);
                itemsToReset.Add(HabItemIds.VidaLey);
            }

            // El certificado de aptitud (EMO) deja de ser válido para el nuevo puesto en
            // cualquiera de estos tres casos — un cambio de obra puro (misma empresa, mismo
            // puesto, misma clasificación) no lo toca:
            //   1) cambio de razón social,
            //   2) cambio de puesto de trabajo,
            //   3) cambio de clasificación ascendente (Oficina Central → Staff/Obra).
            var requiereRevisionAptitud = esCambioEmpresa || esCambioPuesto || esCambioRiesgoAscendente;

            if (requiereRevisionAptitud)
            {
                itemsToReset.Add(HabItemIds.CertAptitud);

                if (proyectoDestino == null)
                {
                    var pidParaEmail = (int?)dto.NuevoProyectoId ?? currentProyectoId;
                    if (pidParaEmail.HasValue)
                        proyectoDestino = await ctx.Project
                            .FirstOrDefaultAsync(p => p.ProjectId == pidParaEmail.Value);
                }

                var esOficinaOStaff =
                    ObraOficinaStaffIds.StaffUOficinaCentral.Contains(worker.ObraOficinaStaffId ?? 0);

                if (esCambioEmpresa)
                {
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
                }

                var motivoAptitud = esCambioRiesgoAscendente
                    ? "• Certificado de Aptitud (EMO nuevo obligatorio — sube de riesgo a Staff/Obra)"
                    : esCambioEmpresa
                        ? "• Certificado de Aptitud (Homologación)"
                        : "• Certificado de Aptitud (revisión por cambio de puesto)";

                pendingEmails.Add((
                    [EmailMedico],
                    $"Cambio de obra — Certificado de Aptitud — {worker.Person?.FullName}",
                    BuildBodyReingreso(worker, proyectoDestino, motivoAptitud)
                ));
            }

            foreach (var v in activas)
            {
                v.FechaFin = fechaCambio;
                v.UpdatedAt = now;
            }

            if (nuevoObraOficinaStaffId != currentObraOficinaStaffId)
                worker.ObraOficinaStaffId = nuevoObraOficinaStaffId;

            // El puesto vigente vive en el trabajador; la vinculación solo guarda el nombre
            // congelado del momento del cambio.
            if (esCambioPuesto)
                worker.PuestoId = dto.PuestoId;

            ctx.WorkerVinculacion.Add(new WorkerVinculacion
            {
                WorkerId = workerId,
                EmpresaId = dto.NuevaEmpresaId ?? currentEmpresaId,
                ProyectoId = dto.NuevoProyectoId,
                Puesto = nuevoPuesto ?? currentPuesto,
                ObraOficinaStaffId = nuevoObraOficinaStaffId,
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
                    now,
                    // La inducción del proyecto destino solo se hereda si ya se indujo ahí; si no,
                    // este mismo cambio de obra la resetea a "Falta" (itemsToReset) y la fila debe
                    // nacer como pendiente. Antes se leía el item 12 global (aún "Aprobado" en este
                    // punto) y la fila quedaba completada=true contradiciendo el reset posterior.
                    yaIndujoEnNuevoProyecto);
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

            // Cambio de empresa: sacar al trabajador de la "nomina" de polizas
            // SCTR/Vida Ley de la empresa anterior. Si no se hace esto, la empresa
            // anterior lo vuelve a aprobar automaticamente cada vez que renueva su
            // poliza mensual, aunque el trabajador ya no trabaje ahi.
            if (esCambioEmpresa && currentEmpresaId.HasValue)
            {
                var vinculosEmpresaAnterior = await ctx.SsSctrVidaLeyWorker
                    .Where(svw => svw.WorkerId == workerId)
                    .Join(ctx.SsSctrVidaley, svw => svw.SctrVidaLeyId, s => s.Id, (svw, s) => new { svw, s.EmpresaId })
                    .Where(x => x.EmpresaId == currentEmpresaId.Value)
                    .Select(x => x.svw)
                    .ToListAsync();

                if (vinculosEmpresaAnterior.Count > 0)
                    ctx.SsSctrVidaLeyWorker.RemoveRange(vinculosEmpresaAnterior);
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

            if (esCambioPuesto)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.CambioPuesto,
                    Descripcion = $"Cambio de puesto: \"{currentPuesto ?? "—"}\" → \"{nuevoPuesto}\". Certificado de aptitud puesto en revisión.",
                    CreatedAt = nowUtc
                });

            if (esCambioRiesgoAscendente)
                ctx.WorkerEvento.Add(new WorkerEvento
                {
                    WorkerId = workerId,
                    TipoEvento = WorkerTipoEvento.CambioRiesgo,
                    Descripcion = $"Cambio de clasificación: \"{ObraOficinaStaffIds.Nombre(currentObraOficinaStaffId) ?? "—"}\" → " +
                                  $"\"{ObraOficinaStaffIds.Nombre(nuevoObraOficinaStaffId)}\". Sube de riesgo — requiere EMO nuevo, no es convalidable.",
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

            // Auto-crear convalidación pendiente si el certificado de aptitud quedó en revisión
            // (cambio de razón social, de puesto, o subida de riesgo Oficina Central → Staff/Obra)
            // y el trabajador tiene un EMO activo: en vez de dejarlo solo bloqueado ("Falta"), se
            // le arma al médico la convalidación lista para que decida — la misma mecánica que ya
            // existía para cambio de empresa, ahora extendida a los otros dos disparadores.
            _logger.LogInformation(
                "[Convalidacion] requiereRevisionAptitud={Requiere} (empresa={Empresa} puesto={Puesto} riesgo={Riesgo}) workerId={WorkerId}",
                requiereRevisionAptitud, esCambioEmpresa, esCambioPuesto, esCambioRiesgoAscendente, workerId);

            if (requiereRevisionAptitud)
            {
                var ultimoEmo = await ctx.WorkerEmo
                    .Where(e => e.WorkerId == workerId && e.Activo)
                    .OrderByDescending(e => e.FechaEmo)
                    .ThenByDescending(e => e.Id)
                    .FirstOrDefaultAsync();

                _logger.LogInformation("[Convalidacion] ultimoEmo={UltimoEmoId}", ultimoEmo?.Id);

                var empresaDestinoResuelta = dto.NuevaEmpresaId ?? currentEmpresaId;

                // Evita apilar una convalidación redundante: si ya existe una Aprobada / Aprobada
                // con Observaciones para el MISMO EMO, hacia la misma empresa y clasificación
                // destino, esta transición ya fue resuelta por un médico antes — no hay nada nuevo
                // que decidir (p.ej. un cambio de obra administrativo que reafirma un destino ya
                // convalidado). En ese caso se restaura el estado directamente en vez de crear otra
                // fila "Pendiente" contra un caso ya cerrado.
                var yaConvalidadoHaciaDestino = ultimoEmo != null && await ctx.WorkerEmoConvalidacion
                    .AnyAsync(cv => cv.EmoId == ultimoEmo.Id
                        && (cv.Resultado == "Aprobada" || cv.Resultado == "Aprobada con Observaciones")
                        && cv.EmpresaDestinoId == empresaDestinoResuelta
                        && cv.ObraOficinaStaffDestinoId == nuevoObraOficinaStaffId);

                var habCert = await ctx.SsHabTrabajador
                    .FirstOrDefaultAsync(h => h.WorkerId == workerId && h.ItemId == HabItemIds.CertAptitud);

                if (yaConvalidadoHaciaDestino && ultimoEmo != null)
                {
                    _logger.LogInformation(
                        "[Convalidacion] destino ya convalidado antes, se registra como Descartada (auditable). emoId={EmoId}",
                        ultimoEmo.Id);

                    // No se deja un cambio de estado invisible: se registra igual una fila en
                    // Convalidaciones (auditable, visible en la lista) pero ya resuelta como
                    // "Descartada" — sin pedir firma, porque no es una decisión médica nueva,
                    // solo se está reafirmando una que ya existía para ese mismo destino.
                    ctx.WorkerEmoConvalidacion.Add(new WorkerEmoConvalidacion
                    {
                        EmoId = ultimoEmo.Id,
                        EmpresaDestinoId = empresaDestinoResuelta,
                        FechaConvalidacion = fechaCambio,
                        Resultado = "Descartada",
                        Observaciones = "Descartada automáticamente: el destino ya tenía una convalidación Aprobada previa.",
                        PuestoOrigen = currentPuesto,
                        PuestoDestino = nuevoPuesto ?? currentPuesto,
                        ObraOficinaStaffOrigenId = currentObraOficinaStaffId,
                        ObraOficinaStaffDestinoId = nuevoObraOficinaStaffId,
                        CambioRiesgo = esCambioRiesgoAscendente,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });

                    if (habCert != null)
                    {
                        habCert.Estado = "Aprobado";
                        habCert.UpdatedAt = DateTime.UtcNow;
                    }
                    ultimoEmo.Estado = "Convalidado";
                    ultimoEmo.UpdatedAt = DateTimeOffset.UtcNow;
                }
                else if (ultimoEmo != null)
                {
                    ctx.WorkerEmoConvalidacion.Add(new WorkerEmoConvalidacion
                    {
                        EmoId = ultimoEmo.Id,
                        // Si no cambia de empresa (solo puesto y/o clasificación), la convalidación
                        // sigue siendo dentro de la misma empresa vigente.
                        EmpresaDestinoId = empresaDestinoResuelta,
                        FechaConvalidacion = fechaCambio,
                        Resultado = "Pendiente",
                        PuestoOrigen = currentPuesto,
                        PuestoDestino = nuevoPuesto ?? currentPuesto,
                        ObraOficinaStaffOrigenId = currentObraOficinaStaffId,
                        ObraOficinaStaffDestinoId = nuevoObraOficinaStaffId,
                        CambioRiesgo = esCambioRiesgoAscendente,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });

                    // Marcar CertAptitud como Pendiente (override del "Falta" ya asignado arriba):
                    // hay un EMO reciente sobre el que el médico puede decidir, no hace falta
                    // bloquear de inmediato exigiendo un EMO nuevo desde cero.
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

                    // Mantiene sincronizado el estado del EMO con el ítem de habilitación: si no se
                    // hace, un trabajador "Casa" puede seguir viéndose "Habilitado" en el listado
                    // (ese cálculo para Casa solo mira WorkerEmo.Estado, no el ítem CertAptitud) aunque
                    // el ítem ya diga "Pendiente"/"Falta" — la causa exacta de la incoherencia vista
                    // en el caso Díaz Díaz (EMO "Falta" pero trabajador "Habilitado").
                    ultimoEmo.Estado = "Pendiente";
                    ultimoEmo.UpdatedAt = DateTimeOffset.UtcNow;
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

            // Solo valida conflicto de empresa cuando el usuario realmente elige una nueva
            // razón social. Si el campo viene null (quiere mantener la actual), no hay
            // cambio real y por tanto no puede haber conflicto contra sí misma.
            if (dto.NuevaEmpresaId.HasValue)
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
                    ObraOficinaStaffIds.StaffUOficinaCentral.Contains(worker.ObraOficinaStaffId ?? 0);

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
                    now,
                    // En un reingreso con cambio de proyecto, la Inducción Obra siempre se resetea a
                    // "Falta" (itemsToReset más abajo), por lo que la fila del proyecto destino debe
                    // nacer como inducción pendiente.
                    induccionCompletadaNuevoProyecto: false);
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

            // Cambio de empresa en el reingreso: mismo cuidado que en CambiarObraAsync —
            // sacar al trabajador de la nomina de polizas SCTR/Vida Ley de la empresa anterior.
            if (esCambioEmpresa && currentEmpresaId.HasValue)
            {
                var vinculosEmpresaAnterior = await ctx.SsSctrVidaLeyWorker
                    .Where(svw => svw.WorkerId == workerId)
                    .Join(ctx.SsSctrVidaley, svw => svw.SctrVidaLeyId, s => s.Id, (svw, s) => new { svw, s.EmpresaId })
                    .Where(x => x.EmpresaId == currentEmpresaId.Value)
                    .Select(x => x.svw)
                    .ToListAsync();

                if (vinculosEmpresaAnterior.Count > 0)
                    ctx.SsSctrVidaLeyWorker.RemoveRange(vinculosEmpresaAnterior);
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
            DateTimeOffset now,
            bool induccionCompletadaNuevoProyecto)
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
                // El flag de inducción del proyecto destino lo decide la operación que llama (según
                // si la inducción se resetea o se conserva para ese proyecto), no el item 12 global.
                previaCerrada.InduccionCompletada = induccionCompletadaNuevoProyecto;
                previaCerrada.FechaInduccion = induccionCompletadaNuevoProyecto
                    ? previaCerrada.FechaInduccion ?? DateOnly.FromDateTime(now.UtcDateTime)
                    : null;
                previaCerrada.UpdatedAt = now;
                return;
            }

            ctx.WorkerProyecto.Add(new WorkerProyecto
            {
                WorkerId = workerId,
                ProyectoId = proyectoNuevoId,
                EmpresaId = empresaNuevaId,
                FechaInicio = fechaCambio,
                FechaFin = null,
                InduccionCompletada = induccionCompletadaNuevoProyecto,
                FechaInduccion = induccionCompletadaNuevoProyecto ? DateOnly.FromDateTime(now.UtcDateTime) : null,
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

            var worker = await ctx.Worker
                .Include(w => w.CategoriaCatalogo)
                .FirstOrDefaultAsync(w => w.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var workerType = string.Equals(worker.ContrataCasa?.Trim(), "Casa", StringComparison.OrdinalIgnoreCase)
                ? "CASA"
                : "CONTRATISTA";

            var todosItems = await ctx.SsItemTrabajador
                .Where(i => i.Activo)
                .ToListAsync();

            var esContratista = string.Equals(worker.ContrataCasa?.Trim(), "Contratista", StringComparison.OrdinalIgnoreCase);
            var categoriaWorker = worker.CategoriaCatalogo?.Nombre;
            var esCasaPracticante = workerType == "CASA"
                && worker.CategoriaId == CategoriaIds.Practicante;

            var itemsAplicables = todosItems
                .Where(i => i.AplicaA == "TODOS" ||
                            (i.AplicaA == "CASA" && workerType == "CASA") ||
                            (i.AplicaA == "CONTRATISTA" && workerType == "CONTRATISTA"))
                .Where(i => CsvContiene(i.AplicaCategoria, categoriaWorker))
                .Where(i => CsvContiene(i.AplicaObraOficina, ObraOficinaStaffIds.Nombre(worker.ObraOficinaStaffId)))
                .Where(i => !CsvExcluye(i.ExcluyeObraOficina, ObraOficinaStaffIds.Nombre(worker.ObraOficinaStaffId)))
                .Where(i => !esContratista || !CsvExcluye(i.ExcluyeCategoriaContratista, categoriaWorker))
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
                .Include(x => x.Person).ThenInclude(p => p!.Sexo)
                .Include(x => x.Contributor)
                .Include(x => x.CategoriaCatalogo)
                .Include(x => x.PuestoCatalogo)
                .FirstOrDefaultAsync(x => x.Id == workerId);
            if (w is null) return null;

            var detalle = MapToDetalle(w);

            // Jefe personalizado vigente: es lo que decide si el formulario abre el campo de
            // revisor como "el que sugiere el sistema" (el del área) o con el jefe elegido a mano.
            var jefe = await _jefePersonalizado.GetAsync(workerId);
            detalle.JefePersonalizadoWorkerId = jefe?.WorkerId;
            detalle.JefePersonalizadoNombre   = jefe?.FullName;
            detalle.JefePersonalizadoEmail    = jefe?.Email;

            return detalle;
        }

        public async Task<WorkerDetalleDto> UpdateAsync(int workerId, WorkerUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();
            var w = await ctx.Worker
                .Include(x => x.Person).ThenInclude(p => p!.Sexo)
                .Include(x => x.Contributor)
                .Include(x => x.CategoriaCatalogo)
                .Include(x => x.PuestoCatalogo)
                .FirstOrDefaultAsync(x => x.Id == workerId)
                ?? throw new AbrilException("Trabajador no encontrado.", 404);

            var categoriaAnterior = w.CategoriaCatalogo?.Nombre;
            var categoriaAnteriorId = w.CategoriaId;
            var obraOficinaAnterior = w.ObraOficinaStaffId;

            if (dto.ApellidoNombre is not null && w.Person is not null) w.Person.FullName = dto.ApellidoNombre;
            if (dto.Celular is not null && w.Person is not null) w.Person.PhoneNumber = int.TryParse(dto.Celular, out var ph) ? ph : (int?)null;
            if (dto.EmailCorporativo is not null)    w.EmailCorporativo = dto.EmailCorporativo;
            if (dto.FechaNacimiento.HasValue && w.Person is not null) w.Person.FechaNacimiento = dto.FechaNacimiento;
            if (dto.FechaIngreso.HasValue) w.FechaIngreso = dto.FechaIngreso;
            if (dto.FechaRetiro.HasValue) w.FechaRetiro = dto.FechaRetiro;
            if (dto.CategoriaId.HasValue) w.CategoriaId = dto.CategoriaId;
            if (dto.PuestoId.HasValue) w.PuestoId = dto.PuestoId;
            if (dto.Area is not null) w.Area = dto.Area;
            if (dto.Subarea is not null) w.Subarea = dto.Subarea;
            if (dto.ContrataCasa is not null) w.ContrataCasa = dto.ContrataCasa;
            if (dto.ObraOficinaStaffId.HasValue) w.ObraOficinaStaffId = dto.ObraOficinaStaffId;
            // Match interno: deriva el nodo normalizado area_scope a partir del texto capturado.
            w.AreaScopeId = Abril_Backend.Shared.Services.AreaScopeMatcher.Resolve(w.Area, w.Subarea);
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
            // La categoría nueva puede no estar cargada todavía en la navegación (se acaba de
            // asignar el id), así que se resuelve su nombre a mano.
            var categoriaNueva = w.CategoriaId == categoriaAnteriorId
                ? categoriaAnterior
                : await ctx.Categoria.Where(c => c.CategoriaId == w.CategoriaId).Select(c => c.Nombre).FirstOrDefaultAsync();

            var eraPracticante = categoriaAnteriorId == CategoriaIds.Practicante;
            var siguePracticante = w.CategoriaId == CategoriaIds.Practicante;
            var transicionFueraDePracticante = dto.CategoriaId is not null && esCasa && eraPracticante && !siguePracticante;

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
            if (dto.ObraOficinaStaffId.HasValue
                && esCasa
                && obraOficinaAnterior != w.ObraOficinaStaffId)
            {
                if (w.ObraOficinaStaffId == ObraOficinaStaffIds.Staff)
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
                else if (w.ObraOficinaStaffId == ObraOficinaStaffIds.OficinaCentral)
                {
                    cambioObraOficinaDestino = "Oficina Central";
                    cambioObraOficinaEmail = EmailAsistentaSocial;
                }
            }

            await ctx.SaveChangesAsync();

            if (vidaLeyCreada)
            {
                var subject = $"Vida Ley pendiente — Cambio de cargo — {w.Person?.FullName}";
                var body = BuildBodyVidaLeyCambioCargo(w, categoriaAnterior, categoriaNueva);
                await EnviarEmailSilenciosoAsync(new List<string> { EmailAsistentaSocial }, subject, body);
            }

            if (cambioObraOficinaDestino is not null && cambioObraOficinaEmail is not null)
            {
                var subject = $"Vida Ley pendiente — Cambio a {cambioObraOficinaDestino} — {w.Person?.FullName}";
                var body = BuildBodyVidaLeyCambioObraOficina(
                    w,
                    ObraOficinaStaffIds.Nombre(obraOficinaAnterior),
                    ObraOficinaStaffIds.Nombre(w.ObraOficinaStaffId));
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
            PersonId = w.PersonId,
            ApellidoNombre = w.Person?.FullName,
            Dni = w.Person?.DocumentIdentityCode,
            Ruc = w.Contributor?.ContributorRuc,
            Celular = w.Person?.PhoneNumber?.ToString(),
            EmailCorporativo = w.EmailCorporativo,
            EmailPersonal = w.Person?.Email,
            FechaNacimiento = w.Person?.FechaNacimiento,
            MostrarEnBoletin = w.Person?.MostrarEnBoletin ?? true,
            Sexo = w.Person?.Sexo != null ? w.Person.Sexo.Codigo : null,
            FechaIngreso = w.FechaIngreso,
            FechaRetiro = w.FechaRetiro,
            CategoriaId = w.CategoriaId,
            Categoria = w.CategoriaCatalogo?.Nombre,
            PuestoId = w.PuestoId,
            Puesto = w.PuestoCatalogo?.Nombre,
            AreaScopeId = w.AreaScopeId,
            Area = w.Area,
            Subarea = w.Subarea,
            ContrataCasa = w.ContrataCasa,
            ObraOficinaStaffId = w.ObraOficinaStaffId,
            ObraOficina = ObraOficinaStaffIds.Nombre(w.ObraOficinaStaffId),
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

            // Trabajador retirado = nadie va a completar una interconsulta pendiente que quedó
            // abierta (caso real: varios trabajadores llevaban 250+ días "Pendiente" ya estando
            // RETIRADO). Se cierra en vez de dejarla huérfana para siempre.
            var interconsultasPendientes = await ctx.SsInterconsulta
                .Where(i => i.WorkerId == workerId && i.Estado == "Pendiente")
                .ToListAsync();
            foreach (var ic in interconsultasPendientes)
            {
                ic.Estado = "Cancelada";
                ic.Diagnostico = string.IsNullOrWhiteSpace(ic.Diagnostico)
                    ? "Cancelada automáticamente: trabajador retirado."
                    : ic.Diagnostico + " | Cancelada automáticamente: trabajador retirado.";
                ic.UpdatedAt = DateTimeOffset.UtcNow;
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

            // Mismo cierre que en BajaAsync: una interconsulta pendiente no la va a completar
            // nadie una vez que el trabajador está retirado.
            var interconsultasPendientes = await ctx.SsInterconsulta
                .Where(i => workerIds.Contains(i.WorkerId) && i.Estado == "Pendiente")
                .ToListAsync();
            foreach (var ic in interconsultasPendientes)
            {
                ic.Estado = "Cancelada";
                ic.Diagnostico = string.IsNullOrWhiteSpace(ic.Diagnostico)
                    ? "Cancelada automáticamente: trabajador retirado."
                    : ic.Diagnostico + " | Cancelada automáticamente: trabajador retirado.";
                ic.UpdatedAt = DateTimeOffset.UtcNow;
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
