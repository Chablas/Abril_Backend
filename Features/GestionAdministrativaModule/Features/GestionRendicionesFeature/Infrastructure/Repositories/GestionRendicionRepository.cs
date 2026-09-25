using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Consolidadores.Interfaces;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Infrastructure.Repositories
{
    /// <summary>
    /// Las planillas de rendición desde el lado del revisor. La visibilidad es exactamente la de
    /// Gestión de Salidas (misma regla, <see cref="SalidaVisibilidadFilter"/>): esta pantalla
    /// muestra las mismas salidas, agrupadas por planilla.
    /// </summary>
    public class GestionRendicionRepository : IGestionRendicionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IJefeRevisorResolver _jefeResolver;
        private readonly IConsolidadorResolver _consolidadorResolver;

        public GestionRendicionRepository(
            IDbContextFactory<AppDbContext> factory,
            IJefeRevisorResolver jefeResolver,
            IConsolidadorResolver consolidadorResolver)
        {
            _factory = factory;
            _jefeResolver = jefeResolver;
            _consolidadorResolver = consolidadorResolver;
        }

        public async Task<List<GestionRendicionListItemDto>> GetAll(GestionRendicionFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, SalidasVisibles(ctx, filters));
            var decido    = await PlanillasQueFirmoAsync(ctx, WorkersPorPlanilla(planillas), filters.CurrentUserId);

            var consolidacion = await ConsolidacionPorPlanillaAsync(ctx, filters.CurrentUserId, planillas);

            var items = planillas.Select(p => Armar(p, decido, consolidacion)).ToList();
            return Filtrar(items, filters);
        }

        public async Task<GestionRendicionDetalleDto?> GetDetalle(int rendicionId, GestionRendicionFiltersDto scope)
        {
            using var ctx = _factory.CreateDbContext();

            var query = SalidasVisibles(ctx, scope).Where(s => s.RendicionId == rendicionId);
            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, query, conDetalle: true);
            if (planillas.Count == 0) return null;

            var planilla = planillas[0];
            var decido   = await PlanillasQueFirmoAsync(ctx, WorkersPorPlanilla(planillas), scope.CurrentUserId);

            var consolidacion = await ConsolidacionPorPlanillaAsync(ctx, scope.CurrentUserId, planillas);

            var cabecera = Armar(planilla, decido, consolidacion);
            var detalle  = new GestionRendicionDetalleDto();
            CopiarCabecera(cabecera, detalle);

            // El pipeline sale de la cabecera ya armada —no de la planilla cruda— para que diga
            // exactamente lo mismo que los badges de arriba del modal.
            detalle.Pipeline = ReembolsoPipelineBuilder.ParaPlanilla(
                codigo:                     cabecera.Codigo,
                rendidoAt:                  cabecera.RendidoAt,
                estadoPrimeraRevision:      cabecera.EstadoPrimeraRevision,
                enviadaRevisionAt:          cabecera.EnviadaRevisionAt,
                primeraRevisionAt:          cabecera.PrimeraRevisionAt,
                consolidado:                cabecera.ConsolidadoS10,
                firmadoAt:                  cabecera.FirmadoAt,
                estadoReembolso:            cabecera.EstadoReembolso,
                observacionOrigen:          cabecera.ObservacionReembolsoOrigen,
                mixto:                      cabecera.ReembolsoMixto);

            detalle.Salidas = planilla.Salidas
                .Select(s => new GestionRendicionSalidaDto
                {
                    Id                   = s.Id,
                    Codigo               = s.Codigo,
                    Trabajador           = s.Trabajador,
                    Area                 = s.Area,
                    FechaSalida          = s.FechaSalida,
                    Motivo               = s.Motivo,
                    LugarOrigen          = s.LugarOrigen,
                    LugarDestino         = s.LugarDestino,
                    TrayectosCount       = s.TrayectosCount,
                    Monto                = s.Monto,
                    EstadoReembolso      = s.EstadoReembolso,
                    ObservacionReembolso = s.ObservacionReembolso,
                })
                .ToList();

            return detalle;
        }

        public async Task<SolicitudSalidaDetalleDto?> GetSalidaDetalle(int solicitudId, GestionRendicionFiltersDto scope)
        {
            using var ctx = _factory.CreateDbContext();

            // Solo una salida rendida y dentro del alcance: es la que el revisor ve en el detalle de
            // la planilla. Mandar un id cualquiera no abre la salida de un área que no le compete.
            var visible = await SalidasVisibles(ctx, SoloVisibilidad(scope))
                .AnyAsync(s => s.Id == solicitudId && s.RendicionId != null);
            if (!visible) return null;

            return await SalidaDetalleLoader.LoadAsync(ctx, solicitudId, conAptitudParaRendir: false);
        }

        public async Task<GestionRendicionFilterDataDto> GetFilterData(GestionRendicionFiltersDto scope)
        {
            using var ctx = _factory.CreateDbContext();

            var seesAll    = scope.SeesAll;
            var areaIds    = scope.VisibleAreaScopeIds ?? new List<int>();
            var deSusObras = scope.TrabajadoresDeSusObras ?? new List<int>();
            var uid        = scope.CurrentUserId;

            // Trabajadores con al menos una salida YA RENDIDA: los que no rindieron nada todavía
            // no tienen planilla, y ofrecerlos en el filtro sería ofrecer un resultado vacío.
            var workerIds = await ctx.GaSolicitudSalida
                .Where(s => s.RendicionId != null)
                .Select(s => s.WorkerId)
                .Distinct()
                .ToListAsync();

            // Mismo alcance que la tabla: su área, él mismo y, si es residente o administrador de
            // obra, los trabajadores de su obra.
            var trabajadoresQuery = ctx.Worker.Where(w => workerIds.Contains(w.Id));
            if (!seesAll)
            {
                trabajadoresQuery = trabajadoresQuery.Where(w =>
                    (w.PuestoCatalogo!.AreaDestinoScopeId != null
                     && areaIds.Contains(w.PuestoCatalogo.AreaDestinoScopeId!.Value))
                    || (uid != null && ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == uid))
                    || deSusObras.Contains(w.Id));
            }

            var trabajadores = await (
                from w   in trabajadoresQuery
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                orderby per != null ? per.FullName : null
                select new TrabajadorOptionDto
                {
                    WorkerId       = w.Id,
                    NombreCompleto = per != null ? (per.FullName ?? "[Sin nombre]") : "[Sin nombre]",
                }
            ).ToListAsync();

            // Las áreas de los trabajadores de su obra también, para poder filtrar por ellas: el
            // frontend toma como raíz a cualquier nodo cuyo padre no vino.
            var areaTree = await (
                from s  in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State
                   && (seesAll
                       || areaIds.Contains(s.AreaScopeId)
                       || ctx.Worker.Any(w => deSusObras.Contains(w.Id)
                                           && w.PuestoCatalogo!.AreaDestinoScopeId == s.AreaScopeId))
                orderby s.DisplayOrder
                select new AreaNodeDto
                {
                    AreaScopeId       = s.AreaScopeId,
                    AreaItemId        = s.AreaItemId,
                    AreaItemName      = ai.AreaItemName,
                    AreaTypeId        = ai.AreaTypeId,
                    AreaTypeName      = at.AreaTypeName,
                    AreaScopeParentId = s.AreaScopeParentId,
                    DisplayOrder      = s.DisplayOrder,
                }
            ).ToListAsync();

            // El periodo de una planilla es el mes de su salida más antigua — el mismo criterio que
            // usa la tabla, si no el filtro dejaría fuera planillas que sí muestra.
            var fechas = await SalidasVisibles(ctx, SoloVisibilidad(scope))
                .Where(s => s.RendicionId != null)
                .Select(s => new { RendicionId = s.RendicionId!.Value, s.FechaSalida })
                .ToListAsync();

            var periodos = fechas
                .GroupBy(x => x.RendicionId)
                .Select(g => g.Min(x => x.FechaSalida))
                .Select(f => (f.Year, f.Month))
                .Distinct()
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .Select(p => new PeriodoRendicionOptionDto
                {
                    Anio  = p.Year,
                    Mes   = p.Month,
                    Label = PlanillaRendicionHelper.EtiquetaMes(p.Year, p.Month),
                })
                .ToList();

            // La razón social bajo la que quedaría un consolidado que suba este usuario: la suya (ver
            // RazonSocialConsolidador). La muestra el modal del Consolidado del S10.
            var razonSocial = uid == null
                ? null
                : (await RazonSocialConsolidador.LoadPorUsuarioAsync(ctx, new[] { uid.Value }))
                    .GetValueOrDefault(uid.Value)?.Nombre;

            return new GestionRendicionFilterDataDto
            {
                Trabajadores = trabajadores,
                AreaTree     = areaTree,
                Periodos     = periodos,
                RazonSocialConsolidador = razonSocial,
            };
        }

        // ══ Primera revisión ════════════════════════════════════════════════

        public async Task<List<int>> DecidirPrimeraRevision(
            IEnumerable<int> rendicionIds, bool aprobar, string? observacion,
            GestionRendicionFiltersDto scope, int reviewerUserId)
        {
            var idsList = rendicionIds?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            if (!aprobar && string.IsNullOrWhiteSpace(observacion))
                throw new AbrilException(
                    "Para observar una rendición hay que escribir el comentario de qué corregir.", 400);

            using var ctx = _factory.CreateDbContext();

            // Solo las planillas de las que el usuario ve alguna salida: mandar un rendicion_id no
            // puede alcanzar planillas de áreas ajenas. Mismo recorte que ResolverSolicitudIds.
            var visibles = await SalidasVisibles(ctx, SoloVisibilidad(scope))
                .Where(s => s.RendicionId != null && idsList.Contains(s.RendicionId!.Value))
                .Select(s => new { RendicionId = s.RendicionId!.Value, s.WorkerId })
                .ToListAsync();

            if (visibles.Count == 0)
                throw new AbrilException("No hay planillas en la selección dentro de tu alcance.", 400);

            var visiblesIds = visibles.Select(x => x.RendicionId).Distinct().ToList();

            var planillas = await ctx.GaRendicion
                .Where(r => visiblesIds.Contains(r.Id)
                         && r.EstadoPrimeraRevisionId == EstadosSalida.PrimeraRevision.EnRevision)
                .ToListAsync();

            if (planillas.Count == 0)
                throw new AbrilException(
                    "Ninguna de las planillas seleccionadas está esperando la primera revisión.", 400);

            // La primera revisión de una planilla la decide su FIRMANTE y nadie más: el mismo que
            // firma su consolidado y recibe sus correos. Ver PlanillasQueFirmoAsync — ver la
            // planilla ya no alcanza para aprobarla.
            var decididasIds = planillas.Select(p => p.Id).ToHashSet();

            var workersPorPlanilla = visibles
                .Where(x => decididasIds.Contains(x.RendicionId))
                .GroupBy(x => x.RendicionId)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<int>)g.Select(x => x.WorkerId).Distinct().ToList());

            var firmo = await PlanillasQueFirmoAsync(ctx, workersPorPlanilla, reviewerUserId);

            if (decididasIds.Any(id => !firmo.Contains(id)))
                throw new AbrilException(
                    "Solo quien firma la planilla puede aprobar u observar su primera revisión. "
                    + "Deselecciona las que no te toquen.", 403);

            var now = DateTimeOffset.UtcNow;
            foreach (var p in planillas)
            {
                p.EstadoPrimeraRevisionId = aprobar
                    ? EstadosSalida.PrimeraRevision.Aprobada
                    : EstadosSalida.PrimeraRevision.Observada;
                p.PrimeraRevisionAt    = now;
                p.PrimeraRevisionPorId = reviewerUserId;
                // Al aprobar se limpia la observación: ya no hay nada que corregir. Al observar se
                // reemplaza por la nueva.
                p.PrimeraRevisionObservacion = aprobar ? null : observacion!.Trim();
            }

            await ctx.SaveChangesAsync();
            return planillas.Select(p => p.Id).ToList();
        }

        public async Task<List<PrimeraRevisionCorreoInfoDto>> GetPrimeraRevisionCorreoInfo(int rendicionId)
        {
            using var ctx = _factory.CreateDbContext();

            var planilla = await ctx.GaRendicion
                .Where(r => r.Id == rendicionId)
                .Select(r => new
                {
                    r.Id, r.Codigo, r.NumeroPlanilla,
                    r.PrimeraRevisionObservacion, r.PrimeraRevisionPorId,
                })
                .FirstOrDefaultAsync();
            if (planilla == null) return new();

            // Todas las salidas de la planilla, sin recorte de visibilidad: el correo va al dueño
            // de cada grupo y el revisor decidió el documento entero.
            var salidas = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join u in ctx.User on (per != null ? per.UserId : null) equals (int?)u.UserId into uGroup
                from u in uGroup.DefaultIfEmpty()
                where s.RendicionId == rendicionId
                select new
                {
                    s.Id,
                    WorkerId    = w.Id,
                    w.Subarea,
                    Trabajador  = per != null ? (per.FullName ?? "Trabajador") : "Trabajador",
                    Email       = u != null ? u.Email : null,
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                    s.FechaSalida,
                }
            ).ToListAsync();

            if (salidas.Count == 0) return new();

            // Monto y trayectos con la misma regla que imprime la columna IMPORTE de la planilla: si
            // el correo dijera otro total, el trabajador no podría contrastarlo con su PDF.
            var solicitudIds = salidas.Select(x => x.Id).ToList();
            var trayectos = await ctx.GaSolicitudTrayecto
                .Where(t => solicitudIds.Contains(t.SolicitudId))
                .Select(t => new { t.Id, t.SolicitudId, t.LugarOrigenId, t.LugarDestinoId })
                .ToListAsync();

            var subareaPorSolicitud = salidas.ToDictionary(x => x.Id, x => x.Subarea);
            var importes = await ImporteRendidoLoader.LoadAsync(
                ctx,
                trayectos
                    .Select(t => new ImporteRendidoLoader.TrayectoParaImporte(
                        t.Id,
                        subareaPorSolicitud.TryGetValue(t.SolicitudId, out var sub) ? sub : null,
                        t.LugarOrigenId,
                        t.LugarDestinoId))
                    .ToList());

            var montoPorSolicitud = trayectos
                .GroupBy(t => t.SolicitudId)
                .ToDictionary(g => g.Key, g => g.Sum(t => importes.TryGetValue(t.Id, out var i) ? i.Importe : 0m));
            // Se cuentan los trayectos rendidos, no los de la salida: los que no generan reembolso
            // no salen impresos en la planilla que el trabajador tiene delante.
            var conteoPorSolicitud = trayectos
                .GroupBy(t => t.SolicitudId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count(t => importes.TryGetValue(t.Id, out var i) && i.EsReembolsable));

            string? decididoPor = null;
            if (planilla.PrimeraRevisionPorId.HasValue)
            {
                decididoPor = await (
                    from w in ctx.Worker
                    join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                    where per.UserId == planilla.PrimeraRevisionPorId.Value
                    select per.FullName
                ).FirstOrDefaultAsync();
            }

            var areaPorScope = new Dictionary<int, string?>();
            foreach (var scopeId in salidas.Where(x => x.AreaScopeId.HasValue)
                                           .Select(x => x.AreaScopeId!.Value).Distinct())
                areaPorScope[scopeId] = await ResolveAreaNombreAsync(ctx, scopeId);

            var codigo = PlanillaRendicionHelper.CodigoRendicion(planilla.Codigo, planilla.Id);

            return salidas
                .GroupBy(x => x.WorkerId)
                .Select(g =>
                {
                    var primera = g.First();
                    var desde   = g.Min(x => x.FechaSalida);
                    var hasta   = g.Max(x => x.FechaSalida);
                    return new PrimeraRevisionCorreoInfoDto
                    {
                        RendicionId      = planilla.Id,
                        Codigo           = codigo,
                        NumeroPlanilla   = PlanillaRendicionHelper.NumeroPlanilla(planilla.NumeroPlanilla),
                        WorkerId         = g.Key,
                        Trabajador       = primera.Trabajador,
                        SolicitanteEmail = primera.Email,
                        Area             = primera.AreaScopeId.HasValue
                                            && areaPorScope.TryGetValue(primera.AreaScopeId.Value, out var a) ? a : null,
                        Periodo          = PlanillaRendicionHelper.EtiquetaPeriodo(desde, hasta),
                        SalidasCount     = g.Count(),
                        TrayectosCount   = g.Sum(x => conteoPorSolicitud.TryGetValue(x.Id, out var tc) ? tc : 0),
                        MontoTotal       = g.Sum(x => montoPorSolicitud.TryGetValue(x.Id, out var m) ? m : 0m),
                        DecididoPor      = decididoPor,
                        Observacion      = planilla.PrimeraRevisionObservacion,
                    };
                })
                .ToList();
        }

        // ══ Reembolso ═══════════════════════════════════════════════════════

        public async Task<PrimeraRevisionPreviewDatos> GetPreviewPrimeraRevision(
            IEnumerable<int> rendicionIds, GestionRendicionFiltersDto scope, bool conTrabajadores)
        {
            var idsList = rendicionIds?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            // Mismo recorte que DecidirPrimeraRevision: solo las planillas de las que el usuario ve
            // alguna salida, y de esas solo las que de verdad están esperando la primera revisión.
            var visibles = await SalidasVisibles(ctx, SoloVisibilidad(scope))
                .Where(s => s.RendicionId != null && idsList.Contains(s.RendicionId!.Value))
                .Select(s => new { s.Id, RendicionId = s.RendicionId!.Value })
                .ToListAsync();
            if (visibles.Count == 0) return new();

            var enRevision = await ctx.GaRendicion
                .Where(r => visibles.Select(x => x.RendicionId).Distinct().Contains(r.Id)
                         && r.EstadoPrimeraRevisionId == EstadosSalida.PrimeraRevision.EnRevision)
                .Select(r => r.Id)
                .ToListAsync();
            if (enRevision.Count == 0) return new();

            var solicitudes = visibles
                .Where(x => enRevision.Contains(x.RendicionId))
                .Select(x => x.Id)
                .ToList();

            var datos = new PrimeraRevisionPreviewDatos
            {
                CorreosSolicitantes = await CorreosSolicitantesAsync(ctx, solicitudes),
            };

            // Los consolidadores se avisan por la planilla entera, como en el envío
            // (GetPrimeraRevisionCorreoInfo no recorta por visibilidad).
            if (conTrabajadores)
                datos.WorkerIds = await ctx.GaSolicitudSalida
                    .Where(s => s.RendicionId != null && enRevision.Contains(s.RendicionId.Value))
                    .Select(s => s.WorkerId)
                    .Distinct()
                    .ToListAsync();

            return datos;
        }

        /// <summary>Correos de los dueños de las salidas indicadas, sin repetir.</summary>
        private static async Task<List<string>> CorreosSolicitantesAsync(
            AppDbContext ctx, ICollection<int> solicitudIds)
        {
            if (solicitudIds.Count == 0) return new();

            return await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                join u in ctx.User on (int?)per.UserId equals (int?)u.UserId
                where solicitudIds.Contains(s.Id) && u.Email != null && u.Email != ""
                select u.Email!
            ).Distinct().ToListAsync();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Salidas del alcance del usuario con los filtros de la pantalla ya aplicados.</summary>
        private static IQueryable<GaSolicitudSalida> SalidasVisibles(
            AppDbContext ctx, GestionRendicionFiltersDto filters)
        {
            var query = ctx.GaSolicitudSalida.AsQueryable();

            if (filters.WorkerId.HasValue)
                query = query.Where(s => s.WorkerId == filters.WorkerId.Value);

            if (filters.FilterAreaScopeIds is { Count: > 0 })
            {
                var areaFilter = filters.FilterAreaScopeIds;
                query = query.Where(s =>
                    ctx.Worker.Any(w => w.Id == s.WorkerId &&
                        w.PuestoCatalogo!.AreaDestinoScopeId != null
                        && areaFilter.Contains(w.PuestoCatalogo.AreaDestinoScopeId!.Value)));
            }

            return SalidaVisibilidadFilter.Aplicar(
                query, ctx, filters.CurrentUserId, filters.SeesAll, filters.VisibleAreaScopeIds,
                filters.TrabajadoresDeSusObras);
        }

        /// <summary>Copia solo el alcance del usuario, sin los filtros de la pantalla.</summary>
        private static GestionRendicionFiltersDto SoloVisibilidad(GestionRendicionFiltersDto scope) => new()
        {
            CurrentUserId          = scope.CurrentUserId,
            SeesAll                = scope.SeesAll,
            VisibleAreaScopeIds    = scope.VisibleAreaScopeIds,
            TrabajadoresDeSusObras = scope.TrabajadoresDeSusObras,
        };


        public async Task<List<int>> GetWorkerIdsDePlanillas(IReadOnlyCollection<int> rendicionIds)
        {
            var ids = rendicionIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();
            return await ctx.GaSolicitudSalida
                .Where(s => s.RendicionId != null && ids.Contains(s.RendicionId.Value))
                .Select(s => s.WorkerId)
                .Distinct()
                .ToListAsync();
        }

        // ── Aviso de rendición consolidada ───────────────────────────────────

        public async Task<List<string>> GetCorreosTrabajadoresDePlanillas(IReadOnlyCollection<int> rendicionIds)
        {
            var ids = rendicionIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();
            return (await SalidasDeTrabajadoresAsync(ctx, ids))
                .Where(s => !string.IsNullOrWhiteSpace(s.Email))
                .Select(s => s.Email!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<RendicionConsolidadaCorreoDatos>> GetRendicionConsolidadaCorreoDatos(int consolidadoId)
        {
            using var ctx = _factory.CreateDbContext();

            // El documento: su código, el número del S10, el área (la del consolidador, la misma de
            // la sigla del código) y quién lo adjuntó.
            var doc = await (
                from c   in ctx.GaConsolidadoS10.AsNoTracking()
                join sc  in ctx.AreaScope on c.AreaScopeId equals (int?)sc.AreaScopeId into scGroup
                from sc  in scGroup.DefaultIfEmpty()
                join ai  in ctx.AreaItem on sc.AreaItemId equals ai.AreaItemId into aiGroup
                from ai  in aiGroup.DefaultIfEmpty()
                join per in ctx.Person on (int?)c.UploadedById equals per.UserId into perGroup
                from per in perGroup.DefaultIfEmpty()
                where c.Id == consolidadoId && c.State
                select new
                {
                    c.Codigo,
                    c.NumeroReembolso,
                    Area         = ai != null ? ai.AreaItemName : null,
                    Consolidador = per != null ? per.FullName : null,
                }
            ).FirstOrDefaultAsync();
            if (doc == null) return new();

            var rendicionIds = await ctx.GaConsolidadoS10Rendicion.AsNoTracking()
                .Where(v => v.State && v.ConsolidadoS10Id == consolidadoId)
                .Select(v => v.RendicionId)
                .Distinct()
                .ToListAsync();
            if (rendicionIds.Count == 0) return new();

            var salidas = await SalidasDeTrabajadoresAsync(ctx, rendicionIds);
            if (salidas.Count == 0) return new();

            var codigoDe = await ctx.GaRendicion.AsNoTracking()
                .Where(r => rendicionIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Codigo })
                .ToDictionaryAsync(r => r.Id, r => PlanillaRendicionHelper.CodigoRendicion(r.Codigo, r.Id));

            var montoPorSalida = await MontoPorSalidaAsync(ctx, salidas);

            return salidas
                .GroupBy(s => new { s.RendicionId, s.WorkerId })
                .Select(g => new RendicionConsolidadaCorreoDatos
                {
                    RendicionId       = g.Key.RendicionId,
                    Codigo            = codigoDe.TryGetValue(g.Key.RendicionId, out var codigo)
                                          ? codigo
                                          : PlanillaRendicionHelper.CodigoRendicion(null, g.Key.RendicionId),
                    TrabajadorEmail   = g.Select(s => s.Email).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)),
                    MontoTrabajador   = g.Sum(s => montoPorSalida.GetValueOrDefault(s.SolicitudId)),
                    ConsolidadoCodigo = doc.Codigo,
                    Area              = doc.Area,
                    NumeroReembolso   = doc.NumeroReembolso,
                    Consolidador      = doc.Consolidador,
                })
                .OrderBy(d => d.Codigo, StringComparer.Ordinal)
                .ToList();
        }

        public async Task<List<RendicionEnPlanillaGrupalCorreoDatos>> GetRendicionEnPlanillaGrupalCorreoDatos(
            int planillaGrupalId)
        {
            using var ctx = _factory.CreateDbContext();

            // La planilla grupal: su código, el área (la de la sigla del código) y quién la preparó.
            var doc = await (
                from g   in ctx.GaPlanillaGrupal.AsNoTracking()
                join sc  in ctx.AreaScope on g.AreaScopeId equals (int?)sc.AreaScopeId into scGroup
                from sc  in scGroup.DefaultIfEmpty()
                join ai  in ctx.AreaItem on sc.AreaItemId equals ai.AreaItemId into aiGroup
                from ai  in aiGroup.DefaultIfEmpty()
                join per in ctx.Person on (int?)g.PreparadaPorId equals per.UserId into perGroup
                from per in perGroup.DefaultIfEmpty()
                where g.Id == planillaGrupalId && g.State
                select new
                {
                    g.Codigo,
                    Area         = ai != null ? ai.AreaItemName : null,
                    Consolidador = per != null ? per.FullName : null,
                }
            ).FirstOrDefaultAsync();
            if (doc == null) return new();

            var rendicionIds = await ctx.GaPlanillaGrupalRendicion.AsNoTracking()
                .Where(v => v.State && v.PlanillaGrupalId == planillaGrupalId)
                .Select(v => v.RendicionId)
                .Distinct()
                .ToListAsync();
            if (rendicionIds.Count == 0) return new();

            var salidas = await SalidasDeTrabajadoresAsync(ctx, rendicionIds);
            if (salidas.Count == 0) return new();

            var codigoDe = await ctx.GaRendicion.AsNoTracking()
                .Where(r => rendicionIds.Contains(r.Id))
                .Select(r => new { r.Id, r.Codigo })
                .ToDictionaryAsync(r => r.Id, r => PlanillaRendicionHelper.CodigoRendicion(r.Codigo, r.Id));

            var montoPorSalida = await MontoPorSalidaAsync(ctx, salidas);

            return salidas
                .GroupBy(s => new { s.RendicionId, s.WorkerId })
                .Select(g => new RendicionEnPlanillaGrupalCorreoDatos
                {
                    RendicionId          = g.Key.RendicionId,
                    Codigo               = codigoDe.TryGetValue(g.Key.RendicionId, out var codigo)
                                             ? codigo
                                             : PlanillaRendicionHelper.CodigoRendicion(null, g.Key.RendicionId),
                    TrabajadorEmail      = g.Select(s => s.Email).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)),
                    MontoTrabajador      = g.Sum(s => montoPorSalida.GetValueOrDefault(s.SolicitudId)),
                    PlanillaGrupalCodigo = doc.Codigo,
                    Area                 = doc.Area,
                    Consolidador         = doc.Consolidador,
                })
                .OrderBy(d => d.Codigo, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Una salida de una planilla, con su trabajador y el correo del usuario de este.</summary>
        private sealed record SalidaDeTrabajador(
            int SolicitudId, int RendicionId, int WorkerId, string? Subarea, string? Email);

        /// <summary>
        /// Las salidas de esas planillas con su trabajador. Es la ÚNICA fuente de a quién se le avisa
        /// que su rendición se consolidó: la usan el preview y el envío, así que la confirmación no
        /// puede prometer direcciones distintas de las que después reciben el correo.
        /// </summary>
        private static async Task<List<SalidaDeTrabajador>> SalidasDeTrabajadoresAsync(
            AppDbContext ctx, List<int> rendicionIds)
        {
            var filas = await (
                from s   in ctx.GaSolicitudSalida.AsNoTracking()
                join w   in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join u   in ctx.User on per.UserId equals (int?)u.UserId into uGroup
                from u   in uGroup.DefaultIfEmpty()
                where s.RendicionId != null && rendicionIds.Contains(s.RendicionId.Value)
                select new
                {
                    s.Id,
                    RendicionId = s.RendicionId!.Value,
                    WorkerId    = w.Id,
                    w.Subarea,
                    Email       = u != null ? u.Email : null,
                }
            ).ToListAsync();

            return filas
                .Select(f => new SalidaDeTrabajador(f.Id, f.RendicionId, f.WorkerId, f.Subarea, f.Email))
                .ToList();
        }

        /// <summary>
        /// Cuánto se rindió en cada salida, con la MISMA regla que imprime la planilla
        /// (<see cref="ImporteRendidoLoader"/>): es el monto que el trabajador ve en su PDF.
        /// </summary>
        private static async Task<Dictionary<int, decimal>> MontoPorSalidaAsync(
            AppDbContext ctx, IReadOnlyCollection<SalidaDeTrabajador> salidas)
        {
            var subareaDe = salidas
                .GroupBy(s => s.SolicitudId)
                .ToDictionary(g => g.Key, g => g.First().Subarea);
            var solicitudIds = subareaDe.Keys.ToList();

            var trayectos = await ctx.GaSolicitudTrayecto.AsNoTracking()
                .Where(t => solicitudIds.Contains(t.SolicitudId))
                .Select(t => new { t.Id, t.SolicitudId, t.LugarOrigenId, t.LugarDestinoId })
                .ToListAsync();

            var importes = await ImporteRendidoLoader.LoadAsync(
                ctx,
                trayectos
                    .Select(t => new ImporteRendidoLoader.TrayectoParaImporte(
                        t.Id, subareaDe.GetValueOrDefault(t.SolicitudId), t.LugarOrigenId, t.LugarDestinoId))
                    .ToList());

            return trayectos
                .GroupBy(t => t.SolicitudId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(t => importes.TryGetValue(t.Id, out var imp) ? imp.Importe : 0m));
        }

        /// <summary>
        /// Lo que la pantalla necesita para ofrecer (o no) la planilla grupal y el Consolidado del S10
        /// de una fila.
        /// </summary>
        private sealed class ConsolidacionFila
        {
            public bool PuedePreparar { get; init; }
            public PlanillaGrupalDto? PlanillaGrupal { get; init; }
            public bool PuedeAdjuntar { get; init; }
            public List<ConsolidadoConjuntoItemDto> Conjunto { get; init; } = new();
            public bool PuedeConsolidar { get; init; }
        }

        /// <summary>
        /// Para cada planilla de la tabla: si admite la planilla grupal y el Consolidado del S10, qué
        /// planillas cubriría el que se adjunte desde ella (su planilla grupal entera, o las de su
        /// consolidado actual, ver <see cref="ConsolidadoS10Agrupacion"/>) y si el usuario puede
        /// consolidar por TODOS los trabajadores de ese conjunto. Son las mismas reglas que validan
        /// la preparación y la subida, así que la pantalla no ofrece nada que el servidor vaya a
        /// rechazar. El permiso sale de <c>IConsolidadorResolver</c>, el mismo que alimenta la
        /// pantalla de Consolidadores: ver una planilla no habilita a hacerle el trámite, y el
        /// propio trabajador ya no consolida lo suyo.
        ///
        /// Un número fijo de consultas para toda la tabla —incluidas las planillas de fuera de la
        /// tabla que comparten planilla grupal o consolidado con alguna fila— y una sola llamada al
        /// resolver.
        /// </summary>
        private async Task<Dictionary<int, ConsolidacionFila>> ConsolidacionPorPlanillaAsync(
            AppDbContext ctx, int? userId, List<PlanillaRendicionLoader.PlanillaFila> planillas)
        {
            if (planillas.Count == 0) return new();

            // La planilla grupal que espera su S10: solo la de las filas sin consolidado. Con el S10
            // subido la planilla grupal viaja dentro del consolidado, con su copia firmada.
            var preparadas = await PlanillaGrupalLoader.LoadPorRendicionAsync(
                ctx, planillas.Where(p => p.ConsolidadoS10 == null).Select(p => p.Id).ToList());

            // Las planillas de la tabla y las que comparten documento con ellas: el S10 se sube para
            // la planilla grupal entera, y un consolidado compartido se reemplaza entero, así que el
            // conjunto de una fila puede traer planillas que la tabla no muestra (otro filtro, otra
            // área).
            var enTabla = planillas.ToDictionary(p => p.Id);
            var involucradas = planillas.Select(p => p.Id)
                .Concat(planillas
                    .Where(p => p.ConsolidadoS10 != null)
                    .SelectMany(p => p.ConsolidadoS10!.Rendiciones)
                    .Select(r => r.Id))
                .Concat(preparadas.Values.SelectMany(g => g.Rendiciones).Select(r => r.Id))
                .Distinct()
                .ToList();

            var agrupables = await ConsolidadoS10Agrupacion.LoadPlanillasAsync(ctx, involucradas);

            // El monto completo de las de la tabla ya viene en la fila; el del resto se calcula.
            var totalesFuera = await TotalPlanillaLoader.LoadAsync(
                ctx, involucradas.Where(id => !enTabla.ContainsKey(id)).ToList());

            var codigoDe = planillas
                .Where(p => p.ConsolidadoS10 != null)
                .SelectMany(p => p.ConsolidadoS10!.Rendiciones)
                .Concat(preparadas.Values.SelectMany(g => g.Rendiciones))
                .GroupBy(r => r.Id)
                .ToDictionary(g => g.Key, g => g.First().Codigo);
            foreach (var planilla in planillas) codigoDe[planilla.Id] = planilla.Codigo;

            // Sin consolidado, el primero se sube sobre la planilla grupal entera (ella primero); con
            // consolidado, lo que se reemplazaría junto con el actual.
            var conjuntos = planillas.ToDictionary(
                p => p.Id,
                p => p.ConsolidadoS10 == null && preparadas.TryGetValue(p.Id, out var grupal)
                    ? grupal.Rendiciones.Select(r => r.Id).OrderBy(id => id == p.Id ? 0 : 1).ToList()
                    : ConsolidadoS10Agrupacion.Conjunto(p.Id, p.ConsolidadoS10, agrupables));

            List<int> TrabajadoresDe(IEnumerable<int> rendicionIds) => rendicionIds
                .SelectMany(id => agrupables.TryGetValue(id, out var a) ? a.WorkerIds : new List<int>())
                .Distinct()
                .ToList();

            var workerIds = TrabajadoresDe(conjuntos.Values.SelectMany(c => c));

            // A quién puede consolidar el usuario y, en la misma resolución, quienes prepararon las
            // planillas grupales de la tabla: la planilla que uno agrupó la sigue solo él
            // (TramiteConsolidador), salvo que ya no pueda consolidar por esa gente.
            var duenos = preparadas.Values.Select(g => g.PreparadaPorId).Where(id => id > 0).Distinct().ToList();
            var habilitados = userId == null || workerIds.Count == 0
                ? new Dictionary<int, HashSet<int>>()
                : await _consolidadorResolver.FiltrarQuePuedenConsolidarAsync(
                    duenos.Append(userId.Value).Distinct().ToList(), workerIds);
            var habilitado = userId != null && habilitados.TryGetValue(userId.Value, out var delUsuario)
                ? delUsuario
                : new HashSet<int>();

            return planillas.ToDictionary(p => p.Id, p =>
            {
                var conjunto     = conjuntos[p.Id];
                var trabajadores = TrabajadoresDe(conjunto);

                // Aprobada, con el reembolso abierto y sin S10: lo que admite la planilla grupal y
                // el primer consolidado. Con un consolidado ya adjunto no: reemplazarlo es de
                // Consolidados.
                var abierta = p.EstadoPrimeraRevisionId == EstadosSalida.PrimeraRevision.Aprobada
                           && p.ConsolidadoS10 == null
                           && agrupables.TryGetValue(p.Id, out var propia) && propia.ReembolsoAbierto;
                preparadas.TryGetValue(p.Id, out var planillaGrupal);

                return new ConsolidacionFila
                {
                    // Una sola vez: la planilla grupal ya preparada no se rehace ni se reemplaza.
                    PuedePreparar  = abierta && planillaGrupal == null,
                    PlanillaGrupal = planillaGrupal,
                    // Solo el primero, y sobre la planilla grupal ya preparada.
                    PuedeAdjuntar  = abierta && planillaGrupal != null,
                    Conjunto = conjunto.Select(id => new ConsolidadoConjuntoItemDto
                    {
                        Id                 = id,
                        Codigo             = codigoDe.TryGetValue(id, out var codigo) ? codigo : $"#{id}",
                        MontoTotalPlanilla = enTabla.TryGetValue(id, out var fila)
                                                ? fila.MontoTotalPlanilla
                                                : totalesFuera.GetValueOrDefault(id),
                    }).ToList(),
                    // El consolidado cubre los documentos enteros: hace falta poder por TODOS los
                    // trabajadores del conjunto, también por los que la tabla no muestra. Y una
                    // planilla grupal ya preparada la sigue quien la preparó.
                    PuedeConsolidar = userId != null && TramiteConsolidador.PuedeSeguir(
                        userId.Value,
                        planillaGrupal?.PreparadaPorId,
                        trabajadores,
                        habilitado,
                        planillaGrupal != null ? habilitados.GetValueOrDefault(planillaGrupal.PreparadaPorId) : null),
                };
            });
        }

        /// <summary>
        /// De las planillas dadas, las que ESTE usuario puede decidir en primera revisión: aquellas
        /// cuyo firmante resuelto es él.
        ///
        /// Cambió de forma el 2026-09-21. Antes era un guard NEGATIVO —"no decides lo tuyo"— y
        /// alcanzaba con ver la planilla para poder aprobarla, así que recepción y GTH, que ven
        /// todo, aprobaban la primera revisión de cualquiera. Ahora la planilla tiene UN firmante
        /// (<c>ResolveFirmantesDeDocumentosAsync</c>), el mismo que firma su consolidado y el mismo
        /// que recibe los correos, y solo él decide.
        ///
        /// Esto NO toca la visibilidad: las filas siguen saliendo de <c>SalidasVisibles</c>, así que
        /// recepción y GTH ven exactamente lo mismo que antes — lo que pierden es el botón, que
        /// nunca fue su trabajo. La visibilidad se administra aparte, en Gestión de Salidas →
        /// Configuración → Visibilidad.
        ///
        /// "Nadie decide lo suyo" ya no hace falta como regla aparte: el algoritmo nunca señala a
        /// alguien de dentro del documento. Solo lo decide quien fue elegido a mano (en su ficha o en
        /// Revisores de Áreas), que es una elección explícita.
        ///
        /// Un número fijo de consultas: las fichas del usuario, un lote para todas las planillas y,
        /// solo si algún firmante es el fallback de GTH, el árbol de áreas.
        /// </summary>
        private async Task<HashSet<int>> PlanillasQueFirmoAsync(
            AppDbContext ctx,
            IReadOnlyDictionary<int, IReadOnlyCollection<int>> workersPorPlanilla,
            int? userId)
        {
            if (!userId.HasValue || workersPorPlanilla.Count == 0) return new();

            var quien = await RevisorDeLaSalida.CargarQuienDecideAsync(ctx, userId);
            if (quien.WorkerIds.Count == 0) return new();

            var firmantes = await _jefeResolver.ResolveAprobadoresDeDocumentosAsync(
                workersPorPlanilla, PasoAprobacion.PrimeraRevision);

            var necesitaArbol = firmantes.Values.Any(
                fs => fs.Count == 0 || fs.Any(f => f.Persona.WorkerId == null));
            var arbol = necesitaArbol
                ? await RevisorDeLaSalida.CargarArbolAsync(ctx)
                : new Dictionary<int, (int? Padre, string Nombre)>();

            // Basta con estar entre los aprobadores: si hacen falta varios, el turno lo valida la
            // escritura. En obra la primera revision la aprueba solo el administrador, asi que casi
            // siempre esta lista tiene un elemento.
            return firmantes
                .Where(kv => kv.Value.Any(f => RevisorDeLaSalida.EsElRevisor(quien, f.Persona, arbol)))
                .Select(kv => kv.Key)
                .ToHashSet();
        }

        /// <summary>Los trabajadores de cada planilla, que es lo que identifica al documento.</summary>
        private static Dictionary<int, IReadOnlyCollection<int>> WorkersPorPlanilla(
            IEnumerable<PlanillaRendicionLoader.PlanillaFila> planillas)
            => planillas.ToDictionary(
                p => p.Id,
                p => (IReadOnlyCollection<int>)p.Salidas.Select(s => s.WorkerId).Distinct().ToList());

        private static async Task<string?> ResolveAreaNombreAsync(AppDbContext ctx, int? areaScopeId)
        {
            if (!areaScopeId.HasValue) return null;
            return await (
                from sc in ctx.AreaScope
                join it in ctx.AreaItem on sc.AreaItemId equals it.AreaItemId
                where sc.AreaScopeId == areaScopeId.Value
                select it.AreaItemName
            ).FirstOrDefaultAsync();
        }

        private static GestionRendicionListItemDto Armar(
            PlanillaRendicionLoader.PlanillaFila p, HashSet<int> planillasQueFirmo,
            IReadOnlyDictionary<int, ConsolidacionFila> consolidacion) => new()
        {
            Id                 = p.Id,
            Codigo             = p.Codigo,
            NumeroPlanilla     = p.NumeroPlanilla,
            RendidoAt          = p.RendidoAt,
            Periodo            = p.Periodo,
            PeriodoAnio        = p.PeriodoAnio,
            PeriodoMes         = p.PeriodoMes,
            Trabajadores       = p.Trabajadores,
            SalidasCount       = p.SalidasCount,
            MontoTotal         = p.MontoTotal,
            MontoTotalPlanilla = p.MontoTotalPlanilla,
            PdfUrl             = p.PdfUrl,
            PdfFilename        = p.PdfFilename,
            PdfFirmadoUrl      = p.PdfFirmadoUrl,
            PdfFirmadoFilename = p.PdfFirmadoFilename,
            FirmadoAt          = p.FirmadoAt,
            ConsolidadoS10     = p.ConsolidadoS10,
            EstadoPrimeraRevision      = p.EstadoPrimeraRevision,
            EnviadaRevisionAt          = p.EnviadaRevisionAt,
            PrimeraRevisionAt          = p.PrimeraRevisionAt,
            PrimeraRevisionObservacion = p.PrimeraRevisionObservacion,
            PorPrimeraRevision         = p.EstadoPrimeraRevisionId == EstadosSalida.PrimeraRevision.EnRevision,
            EstadoReembolso    = p.EstadoReembolso,
            ReembolsoMixto     = p.ReembolsoMixto,
            ObservacionReembolso = p.ObservacionReembolso,
            ObservacionReembolsoOrigen = EstadosSalida.OrigenObservacionReembolso.Nombre(
                p.ObservacionReembolsoOrigenId),
            RevisorNotificadoAt  = p.RevisorNotificadoAt,
            // La primera revisión es del documento completo y la decide su firmante: no se
            // aprueba "a medias" ni la aprueba cualquiera que la vea.
            PuedeDecidir       = planillasQueFirmo.Contains(p.Id),
            // Lo que resolvió ConsolidacionPorPlanillaAsync: su planilla grupal, qué cubriría el
            // consolidado de esta fila, si se le puede preparar la planilla o adjuntar el S10 y si el
            // usuario puede consolidar por TODOS sus trabajadores.
            PlanillaGrupal           = consolidacion[p.Id].PlanillaGrupal,
            PuedeConsolidar          = consolidacion[p.Id].PuedeConsolidar,
            PuedePrepararPlanilla    = consolidacion[p.Id].PuedePreparar,
            PuedeAdjuntarConsolidado = consolidacion[p.Id].PuedeAdjuntar,
            ConsolidadoConjunto      = consolidacion[p.Id].Conjunto,
        };

        private static void CopiarCabecera(GestionRendicionListItemDto o, GestionRendicionDetalleDto d)
        {
            d.Id = o.Id; d.Codigo = o.Codigo; d.NumeroPlanilla = o.NumeroPlanilla; d.RendidoAt = o.RendidoAt;
            d.Periodo = o.Periodo; d.PeriodoAnio = o.PeriodoAnio; d.PeriodoMes = o.PeriodoMes;
            d.Trabajadores = o.Trabajadores; d.SalidasCount = o.SalidasCount; d.MontoTotal = o.MontoTotal;
            d.MontoTotalPlanilla = o.MontoTotalPlanilla;
            d.PdfUrl = o.PdfUrl; d.PdfFilename = o.PdfFilename;
            d.PdfFirmadoUrl = o.PdfFirmadoUrl; d.PdfFirmadoFilename = o.PdfFirmadoFilename;
            d.FirmadoAt = o.FirmadoAt; d.ConsolidadoS10 = o.ConsolidadoS10;
            d.PlanillaGrupal = o.PlanillaGrupal;
            d.EstadoPrimeraRevision = o.EstadoPrimeraRevision; d.EnviadaRevisionAt = o.EnviadaRevisionAt;
            d.PrimeraRevisionAt = o.PrimeraRevisionAt;
            d.PrimeraRevisionObservacion = o.PrimeraRevisionObservacion;
            d.PorPrimeraRevision = o.PorPrimeraRevision;
            d.EstadoReembolso = o.EstadoReembolso; d.ReembolsoMixto = o.ReembolsoMixto;
            d.ObservacionReembolso = o.ObservacionReembolso;
            d.ObservacionReembolsoOrigen = o.ObservacionReembolsoOrigen;
            d.RevisorNotificadoAt = o.RevisorNotificadoAt;
            d.PuedeDecidir = o.PuedeDecidir; d.PuedeConsolidar = o.PuedeConsolidar;
            d.PuedePrepararPlanilla = o.PuedePrepararPlanilla;
            d.PuedeAdjuntarConsolidado = o.PuedeAdjuntarConsolidado;
            d.ConsolidadoConjunto = o.ConsolidadoConjunto;
        }

        /// <summary>
        /// Filtros que se resuelven sobre la fila ya armada (el estado y el periodo de una planilla
        /// salen de sus salidas, así que no se pueden pedir en la consulta).
        /// </summary>
        private static List<GestionRendicionListItemDto> Filtrar(
            List<GestionRendicionListItemDto> items, GestionRendicionFiltersDto filters)
        {
            IEnumerable<GestionRendicionListItemDto> q = items;

            if (!string.IsNullOrWhiteSpace(filters.EstadoPrimeraRevision))
                q = q.Where(x => x.EstadoPrimeraRevision == filters.EstadoPrimeraRevision!.Trim());

            if (!string.IsNullOrWhiteSpace(filters.EstadoReembolso))
                q = q.Where(x => x.EstadoReembolso == filters.EstadoReembolso!.Trim());

            if (filters.ConConsolidado.HasValue)
                q = q.Where(x => (x.ConsolidadoS10 != null) == filters.ConConsolidado.Value);

            if (filters.PeriodoAnio.HasValue && filters.PeriodoMes.HasValue)
                q = q.Where(x => x.PeriodoAnio == filters.PeriodoAnio.Value
                              && x.PeriodoMes  == filters.PeriodoMes.Value);

            return q.ToList();
        }
    }
}
