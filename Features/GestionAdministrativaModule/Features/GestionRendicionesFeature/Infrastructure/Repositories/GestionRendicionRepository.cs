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
            var ajenas    = await MisWorkerIdsQueNoDecidoAsync(ctx, filters.CurrentUserId);

            var consolidacion = await ConsolidacionPorPlanillaAsync(ctx, filters.CurrentUserId, planillas);

            var items = planillas.Select(p => Armar(p, ajenas, consolidacion)).ToList();
            return Filtrar(items, filters);
        }

        public async Task<GestionRendicionDetalleDto?> GetDetalle(int rendicionId, GestionRendicionFiltersDto scope)
        {
            using var ctx = _factory.CreateDbContext();

            var query = SalidasVisibles(ctx, scope).Where(s => s.RendicionId == rendicionId);
            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, query, conDetalle: true);
            if (planillas.Count == 0) return null;

            var planilla = planillas[0];
            var ajenas   = await MisWorkerIdsQueNoDecidoAsync(ctx, scope.CurrentUserId);

            var consolidacion = await ConsolidacionPorPlanillaAsync(ctx, scope.CurrentUserId, planillas);

            var cabecera = Armar(planilla, ajenas, consolidacion);
            var detalle  = new GestionRendicionDetalleDto();
            CopiarCabecera(cabecera, detalle);

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

            var seesAll  = scope.SeesAll;
            var areaIds  = scope.VisibleAreaScopeIds ?? new List<int>();
            var uid      = scope.CurrentUserId;

            // Trabajadores con al menos una salida YA RENDIDA: los que no rindieron nada todavía
            // no tienen planilla, y ofrecerlos en el filtro sería ofrecer un resultado vacío.
            var workerIds = await ctx.GaSolicitudSalida
                .Where(s => s.RendicionId != null)
                .Select(s => s.WorkerId)
                .Distinct()
                .ToListAsync();

            var trabajadoresQuery = ctx.Worker.Where(w => workerIds.Contains(w.Id));
            if (!seesAll)
            {
                trabajadoresQuery = trabajadoresQuery.Where(w =>
                    (w.PuestoCatalogo!.AreaDestinoScopeId != null
                     && areaIds.Contains(w.PuestoCatalogo.AreaDestinoScopeId!.Value))
                    || (uid != null && ctx.Person.Any(p => p.PersonId == w.PersonId && p.UserId == uid)));
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

            var areaTree = await (
                from s  in ctx.AreaScope
                join ai in ctx.AreaItem on s.AreaItemId equals ai.AreaItemId
                join at in ctx.AreaType on ai.AreaTypeId equals at.AreaTypeId
                where s.State && ai.State && at.State
                   && (seesAll || areaIds.Contains(s.AreaScopeId))
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

            // Nadie revisa su propia rendición, salvo el que es su propio revisor (jefe
            // personalizado apuntándose a sí mismo). Misma regla que la decisión del reembolso y
            // que aprobar la salida; ver MisWorkerIdsQueNoDecidoAsync.
            var ajenas = await MisWorkerIdsQueNoDecidoAsync(ctx, reviewerUserId);

            var decididasIds = planillas.Select(p => p.Id).ToHashSet();
            if (visibles.Any(x => decididasIds.Contains(x.RendicionId) && ajenas.Contains(x.WorkerId)))
                throw new AbrilException(
                    "No puedes revisar una rendición con tus propias salidas — deselecciónala primero.", 403);

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
                query, ctx, filters.CurrentUserId, filters.SeesAll, filters.VisibleAreaScopeIds);
        }

        /// <summary>Copia solo el alcance del usuario, sin los filtros de la pantalla.</summary>
        private static GestionRendicionFiltersDto SoloVisibilidad(GestionRendicionFiltersDto scope) => new()
        {
            CurrentUserId       = scope.CurrentUserId,
            SeesAll             = scope.SeesAll,
            VisibleAreaScopeIds = scope.VisibleAreaScopeIds,
        };

        private static async Task<HashSet<int>> MisWorkerIdsAsync(AppDbContext ctx, int? userId)
        {
            if (!userId.HasValue) return new();
            var ids = await (
                from w in ctx.Worker
                join per in ctx.Person on w.PersonId equals per.PersonId
                where per.UserId == userId.Value
                select w.Id
            ).ToListAsync();
            return ids.ToHashSet();
        }

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

        /// <summary>Lo que la pantalla necesita para ofrecer (o no) el Consolidado del S10 de una fila.</summary>
        private sealed class ConsolidacionFila
        {
            public bool PuedeAdjuntar { get; init; }
            public List<ConsolidadoConjuntoItemDto> Conjunto { get; init; } = new();
            public bool PuedeConsolidar { get; init; }
        }

        /// <summary>
        /// Para cada planilla de la tabla: si admite el Consolidado del S10, qué planillas cubriría
        /// el que se adjunte desde ella (su conjunto, ver <see cref="ConsolidadoS10Agrupacion"/>) y
        /// si el usuario puede consolidar por TODOS los trabajadores de ese conjunto. Son las mismas
        /// reglas que valida la subida, así que la pantalla no ofrece nada que el servidor vaya a
        /// rechazar. El permiso sale de <c>IConsolidadorResolver</c>, el mismo que alimenta la
        /// pantalla de Consolidadores: ver una planilla no habilita a hacerle el trámite, y el
        /// propio trabajador ya no consolida lo suyo.
        ///
        /// Un número fijo de consultas para toda la tabla —incluidas las planillas de fuera de la
        /// tabla que cuelgan de un consolidado compartido— y una sola llamada al resolver.
        /// </summary>
        private async Task<Dictionary<int, ConsolidacionFila>> ConsolidacionPorPlanillaAsync(
            AppDbContext ctx, int? userId, List<PlanillaRendicionLoader.PlanillaFila> planillas)
        {
            if (planillas.Count == 0) return new();

            // Las planillas de la tabla y las demás de sus consolidados actuales: un consolidado
            // compartido se reemplaza entero, así que el conjunto de una fila puede traer planillas
            // que la tabla no muestra (otro filtro, otra área).
            var enTabla = planillas.ToDictionary(p => p.Id);
            var involucradas = planillas.Select(p => p.Id)
                .Concat(planillas
                    .Where(p => p.ConsolidadoS10 != null)
                    .SelectMany(p => p.ConsolidadoS10!.Rendiciones)
                    .Select(r => r.Id))
                .Distinct()
                .ToList();

            var agrupables = await ConsolidadoS10Agrupacion.LoadPlanillasAsync(ctx, involucradas);

            // El monto completo de las de la tabla ya viene en la fila; el del resto se calcula.
            var totalesFuera = await TotalPlanillaLoader.LoadAsync(
                ctx, involucradas.Where(id => !enTabla.ContainsKey(id)).ToList());

            var codigoDe = planillas
                .Where(p => p.ConsolidadoS10 != null)
                .SelectMany(p => p.ConsolidadoS10!.Rendiciones)
                .GroupBy(r => r.Id)
                .ToDictionary(g => g.Key, g => g.First().Codigo);
            foreach (var planilla in planillas) codigoDe[planilla.Id] = planilla.Codigo;

            var conjuntos = planillas.ToDictionary(
                p => p.Id, p => ConsolidadoS10Agrupacion.Conjunto(p.Id, p.ConsolidadoS10, agrupables));

            List<int> TrabajadoresDe(IEnumerable<int> rendicionIds) => rendicionIds
                .SelectMany(id => agrupables.TryGetValue(id, out var a) ? a.WorkerIds : new List<int>())
                .Distinct()
                .ToList();

            var workerIds = TrabajadoresDe(conjuntos.Values.SelectMany(c => c));

            var habilitado = userId == null || workerIds.Count == 0
                ? new HashSet<int>()
                : await _consolidadorResolver.FiltrarQuePuedeConsolidarAsync(userId.Value, workerIds);

            return planillas.ToDictionary(p => p.Id, p =>
            {
                var conjunto     = conjuntos[p.Id];
                var trabajadores = TrabajadoresDe(conjunto);

                return new ConsolidacionFila
                {
                    PuedeAdjuntar = p.EstadoPrimeraRevisionId == EstadosSalida.PrimeraRevision.Aprobada
                                 && agrupables.TryGetValue(p.Id, out var propia) && propia.ReembolsoAbierto,
                    Conjunto = conjunto.Select(id => new ConsolidadoConjuntoItemDto
                    {
                        Id                 = id,
                        Codigo             = codigoDe.TryGetValue(id, out var codigo) ? codigo : $"#{id}",
                        MontoTotalPlanilla = enTabla.TryGetValue(id, out var fila)
                                                ? fila.MontoTotalPlanilla
                                                : totalesFuera.GetValueOrDefault(id),
                    }).ToList(),
                    // El consolidado cubre los documentos enteros: hace falta poder por TODOS los
                    // trabajadores del conjunto, también por los que la tabla no muestra.
                    PuedeConsolidar = trabajadores.Count > 0 && trabajadores.All(habilitado.Contains),
                };
            });
        }

        /// <summary>
        /// Fichas del usuario cuyas salidas NO le toca decidir a él: la regla es "nadie decide lo
        /// suyo", y la única excepción es que el revisor resuelto de esa ficha sea él mismo, o sea
        /// que tenga el <b>jefe personalizado apuntándose a sí mismo</b> (Gestión de Ingresos →
        /// ficha del trabajador → "Jefe personalizado").
        ///
        /// La excepción no puede abrirse sin querer: el revisor que se deriva del área nunca es el
        /// propio trabajador (lo descarta <c>JefeRevisorResolver</c> al subir por el árbol), así
        /// que solo la abre esa elección explícita. Y se pregunta al MISMO resolver que decide a
        /// quién se le manda el correo de la primera revisión, así que en la web decide exactamente
        /// quien recibe ese correo — mismo criterio que <c>EnsurePuedeDecidirAsync</c> usa para
        /// aprobar/rechazar la salida en Gestión de Salidas.
        ///
        /// Devuelve un conjunto (no un booleano) porque el usuario puede tener varias fichas por
        /// reingreso y el jefe personalizado puede estar puesto en una sola: la ficha con el revisor
        /// propio se decide, las otras no.
        /// </summary>
        private async Task<HashSet<int>> MisWorkerIdsQueNoDecidoAsync(AppDbContext ctx, int? userId)
        {
            var mios = await MisWorkerIdsAsync(ctx, userId);
            if (mios.Count == 0) return mios;

            var revisores = await _jefeResolver.ResolveManyAsync(mios.ToList());

            return mios
                .Where(id => !(revisores.TryGetValue(id, out var revisor)
                               && revisor.WorkerId != null
                               && mios.Contains(revisor.WorkerId.Value)))
                .ToHashSet();
        }

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
            PlanillaRendicionLoader.PlanillaFila p, HashSet<int> misWorkerIdsQueNoDecido,
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
            // Basta una salida suya que no le toque decidir para apagar la planilla entera: la
            // primera revisión es del documento completo, no se puede aprobar "a medias".
            PuedeDecidir       = !p.Salidas.Any(s => misWorkerIdsQueNoDecido.Contains(s.WorkerId)),
            // Lo que resolvió ConsolidacionPorPlanillaAsync: qué cubriría el consolidado de esta
            // fila, si se le puede adjuntar y si el usuario puede consolidar por TODOS sus trabajadores.
            PuedeConsolidar          = consolidacion[p.Id].PuedeConsolidar,
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
            d.EstadoPrimeraRevision = o.EstadoPrimeraRevision; d.EnviadaRevisionAt = o.EnviadaRevisionAt;
            d.PrimeraRevisionAt = o.PrimeraRevisionAt;
            d.PrimeraRevisionObservacion = o.PrimeraRevisionObservacion;
            d.PorPrimeraRevision = o.PorPrimeraRevision;
            d.EstadoReembolso = o.EstadoReembolso; d.ReembolsoMixto = o.ReembolsoMixto;
            d.ObservacionReembolso = o.ObservacionReembolso;
            d.ObservacionReembolsoOrigen = o.ObservacionReembolsoOrigen;
            d.RevisorNotificadoAt = o.RevisorNotificadoAt;
            d.PuedeDecidir = o.PuedeDecidir; d.PuedeConsolidar = o.PuedeConsolidar;
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
