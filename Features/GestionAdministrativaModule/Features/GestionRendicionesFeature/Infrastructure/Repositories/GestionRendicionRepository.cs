using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
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

        public GestionRendicionRepository(
            IDbContextFactory<AppDbContext> factory,
            IJefeRevisorResolver jefeResolver)
        {
            _factory = factory;
            _jefeResolver = jefeResolver;
        }

        public async Task<List<GestionRendicionListItemDto>> GetAll(GestionRendicionFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, SalidasVisibles(ctx, filters));
            var ajenas    = await MisWorkerIdsQueNoDecidoAsync(ctx, filters.CurrentUserId);
            var porDecidir = await IdsConReembolsoRevisableAsync(
                ctx, planillas.SelectMany(p => p.Salidas).Select(s => s.Id).ToList());

            var items = planillas.Select(p => Armar(p, ajenas, porDecidir)).ToList();
            return Filtrar(items, filters);
        }

        public async Task<GestionRendicionDetalleDto?> GetDetalle(int rendicionId, GestionRendicionFiltersDto scope)
        {
            using var ctx = _factory.CreateDbContext();

            var query = SalidasVisibles(ctx, scope).Where(s => s.RendicionId == rendicionId);
            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, query, conDetalle: true);
            if (planillas.Count == 0) return null;

            var planilla   = planillas[0];
            var ajenas     = await MisWorkerIdsQueNoDecidoAsync(ctx, scope.CurrentUserId);
            var porDecidir = await IdsConReembolsoRevisableAsync(ctx, planilla.Salidas.Select(s => s.Id).ToList());

            var cabecera = Armar(planilla, ajenas, porDecidir);
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

            return new GestionRendicionFilterDataDto
            {
                Trabajadores = trabajadores,
                AreaTree     = areaTree,
                Periodos     = periodos,
            };
        }

        public async Task<List<int>> ResolverSolicitudIds(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds, GestionRendicionFiltersDto scope)
        {
            var rIds = rendicionIds?.Distinct().ToList() ?? new List<int>();
            var sIds = solicitudIds?.Distinct().ToList() ?? new List<int>();
            if (rIds.Count == 0 && sIds.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            // El recorte por visibilidad se aplica igual a las dos vías: mandar un rendicion_id no
            // puede alcanzar salidas de áreas que el usuario no ve, aunque cuelguen de la misma
            // planilla.
            return await SalidasVisibles(ctx, SoloVisibilidad(scope))
                .Where(s => s.RendicionId != null
                         && (rIds.Contains(s.RendicionId!.Value) || sIds.Contains(s.Id)))
                .Select(s => s.Id)
                .ToListAsync();
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

            // Monto y tramos con la misma regla que imprime la columna IMPORTE de la planilla: si
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
            var tramosPorSolicitud = trayectos
                .GroupBy(t => t.SolicitudId)
                .ToDictionary(g => g.Key, g => g.Count());

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
                        TramosCount      = g.Sum(x => tramosPorSolicitud.TryGetValue(x.Id, out var tc) ? tc : 0),
                        MontoTotal       = g.Sum(x => montoPorSolicitud.TryGetValue(x.Id, out var m) ? m : 0m),
                        DecididoPor      = decididoPor,
                        Observacion      = planilla.PrimeraRevisionObservacion,
                    };
                })
                .ToList();
        }

        // ══ Reembolso ═══════════════════════════════════════════════════════

        public async Task<List<int>> ObservarReembolso(
            IEnumerable<int> ids, string observacion, int reviewerUserId)
        {
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            if (string.IsNullOrWhiteSpace(observacion))
                throw new AbrilException("Para observar un reembolso hay que escribir la observación.", 400);

            using var ctx = _factory.CreateDbContext();

            var solicitudes = await SalidasDecidiblesAsync(ctx, idsList, reviewerUserId);

            var now = DateTimeOffset.UtcNow;
            var obs = observacion.Trim();

            foreach (var s in solicitudes)
            {
                s.EstadoReembolsoId      = EstadosSalida.Reembolso.Observado;
                s.ReembolsoDecididoPorId = reviewerUserId;
                s.ReembolsoDecididoAt    = now;
                s.UpdatedAt              = now;
                s.ObservacionReembolso   = obs;
            }

            await ctx.SaveChangesAsync();
            return solicitudes.Select(s => s.Id).ToList();
        }

        public async Task<List<PlanillaParaFirmarDto>> GetPlanillasParaAprobarReembolso(
            IEnumerable<int> ids, int reviewerUserId)
        {
            var idsList = ids?.Distinct().ToList() ?? new List<int>();
            if (idsList.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            var solicitudes = await SalidasDecidiblesAsync(ctx, idsList, reviewerUserId);

            var porRendicion = solicitudes
                .Where(s => s.RendicionId != null)
                .GroupBy(s => s.RendicionId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(s => s.Id).ToList());

            if (porRendicion.Count == 0) return new();

            var rendicionIds = porRendicion.Keys.ToList();

            var planillas = await ctx.GaRendicion
                .Where(r => rendicionIds.Contains(r.Id))
                .Select(r => new { r.Id, r.PdfUrl, r.PdfFilename })
                .ToListAsync();

            // El consolidado de cada salida con la MISMA precedencia que usa todo el módulo (el
            // propio de la salida si lo tiene, si no el de su planilla): así una planilla vieja con
            // consolidados por salida también se firma, sin un caso especial acá.
            var consolidadoPorSolicitud = await ConsolidadoS10Loader.LoadAsync(
                ctx, solicitudes.ToDictionary(s => s.Id, s => s.RendicionId));

            var consolidadoIds = consolidadoPorSolicitud.Values.Select(c => c.Id).Distinct().ToList();
            var consolidados = await ctx.GaConsolidadoS10
                .Where(c => consolidadoIds.Contains(c.Id))
                .Select(c => new { c.Id, c.PdfUrl, c.PdfFilename })
                .ToDictionaryAsync(c => c.Id, c => c);

            return planillas.Select(p =>
            {
                var solicitudIds = porRendicion[p.Id];

                var docs = solicitudIds
                    .Select(sid => consolidadoPorSolicitud.TryGetValue(sid, out var dto) ? dto.Id : (int?)null)
                    .Where(cid => cid != null)
                    .Select(cid => cid!.Value)
                    .Distinct()
                    .Where(cid => consolidados.ContainsKey(cid))
                    .Select(cid => new DocumentoParaFirmarDto
                    {
                        Id       = cid,
                        Url      = consolidados[cid].PdfUrl,
                        Filename = consolidados[cid].PdfFilename,
                    })
                    .ToList();

                return new PlanillaParaFirmarDto
                {
                    RendicionId      = p.Id,
                    SolicitudIds     = solicitudIds,
                    PlanillaUrl      = p.PdfUrl,
                    PlanillaFilename = p.PdfFilename,
                    Consolidados     = docs,
                };
            }).ToList();
        }

        public async Task<List<int>> AprobarReembolsoFirmado(
            IReadOnlyCollection<PlanillaFirmadaDto> planillas, int reviewerUserId)
        {
            if (planillas.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();
            var now = DateTimeOffset.UtcNow;

            var solicitudIds = planillas.SelectMany(p => p.SolicitudIds).Distinct().ToList();
            var rendicionIds = planillas.Select(p => p.RendicionId).Distinct().ToList();

            var rendiciones = await ctx.GaRendicion
                .Where(r => rendicionIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id);

            var consolidadoIds = planillas.SelectMany(p => p.Consolidados.Keys).Distinct().ToList();
            var consolidados = await ctx.GaConsolidadoS10
                .Where(c => consolidadoIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            // Se relee el estado en la escritura: entre el guard y la subida a SharePoint pudo
            // decidirse la misma salida desde otra pantalla.
            var solicitudes = await ctx.GaSolicitudSalida
                .Where(s => solicitudIds.Contains(s.Id)
                         && (s.EstadoReembolsoId == EstadosSalida.Reembolso.Pendiente
                          || s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado))
                .ToListAsync();

            var vivas = solicitudes.Select(s => s.Id).ToHashSet();

            foreach (var p in planillas)
            {
                // Si ninguna de sus salidas sigue viva, la planilla no se toca: sus PDF firmados
                // quedan en SharePoint sin referencia, que es mejor que pisar una firma ajena.
                if (!p.SolicitudIds.Any(vivas.Contains)) continue;

                if (rendiciones.TryGetValue(p.RendicionId, out var r))
                {
                    r.PdfFirmadoUrl      = p.Planilla.Url;
                    r.PdfFirmadoItemId   = p.Planilla.ItemId;
                    r.PdfFirmadoFilename = p.Planilla.Filename;
                    r.FirmadoPorId       = reviewerUserId;
                    r.FirmadoAt          = now;
                }

                foreach (var (consolidadoId, archivo) in p.Consolidados)
                {
                    if (!consolidados.TryGetValue(consolidadoId, out var c)) continue;
                    c.PdfFirmadoUrl      = archivo.Url;
                    c.PdfFirmadoItemId   = archivo.ItemId;
                    c.PdfFirmadoFilename = archivo.Filename;
                    c.FirmadoPorId       = reviewerUserId;
                    c.FirmadoAt          = now;
                }
            }

            foreach (var s in solicitudes)
            {
                // Aprobar ES la firma: la salida salta directo a Firmado, que es lo que Tesorería
                // ve como pagable. "Aprobado" ya no es un estado por el que se pase.
                s.EstadoReembolsoId      = EstadosSalida.Reembolso.Firmado;
                s.ReembolsoDecididoPorId = reviewerUserId;
                s.ReembolsoDecididoAt    = now;
                s.FirmadoPorId           = reviewerUserId;
                s.FirmadoAt              = now;
                s.UpdatedAt              = now;
                // Al aprobar se limpia la observación: ya no hay nada que subsanar.
                s.ObservacionReembolso   = null;
            }

            await ctx.SaveChangesAsync();
            return solicitudes.Select(s => s.Id).ToList();
        }

        /// <summary>
        /// Las salidas de la selección cuyo reembolso el usuario puede decidir hoy: elegibles
        /// (rendidas, con Consolidado del S10 y sin decidir) y ninguna suya que no le toque.
        /// Lanza 400/403 con el mismo mensaje que veía la pantalla; la comparte la aprobación y el
        /// rechazo para que las dos apliquen exactamente la misma regla.
        /// </summary>
        private async Task<List<GaSolicitudSalida>> SalidasDecidiblesAsync(
            AppDbContext ctx, List<int> idsList, int reviewerUserId)
        {
            var elegibles = await IdsConReembolsoRevisableAsync(ctx, idsList);
            if (elegibles.Count == 0)
                throw new AbrilException(
                    "Ninguna de las salidas seleccionadas tiene un reembolso por decidir: deben estar " +
                    "rendidas y con el Consolidado del S10 adjunto.", 400);

            var solicitudes = await ctx.GaSolicitudSalida
                .Where(s => elegibles.Contains(s.Id))
                .ToListAsync();

            // Nadie decide el reembolso de sus propias salidas, salvo el que es su propio revisor
            // (jefe personalizado apuntándose a sí mismo). Ver MisWorkerIdsQueNoDecidoAsync.
            var ajenas = await MisWorkerIdsQueNoDecidoAsync(ctx, reviewerUserId);

            if (solicitudes.Any(x => ajenas.Contains(x.WorkerId)))
                throw new AbrilException(
                    "No puedes decidir el reembolso de tus propias salidas — deselecciónalas primero.", 403);

            return solicitudes;
        }

        public async Task<List<string>> GetCorreosSolicitantesPorDecidir(
            IEnumerable<int> rendicionIds, IEnumerable<int> solicitudIds, GestionRendicionFiltersDto scope)
        {
            // El recorte por visibilidad es el mismo que hace la escritura: sin esto, el preview de
            // un rendicion_id delataría los correos de trabajadores de áreas que el usuario no ve.
            var visibles = await ResolverSolicitudIds(rendicionIds, solicitudIds, scope);
            if (visibles.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            var porDecidir = await IdsConReembolsoRevisableAsync(ctx, visibles);
            if (porDecidir.Count == 0) return new();

            return await CorreosSolicitantesAsync(ctx, porDecidir);
        }

        public async Task<List<string>> GetCorreosSolicitantesPrimeraRevision(
            IEnumerable<int> rendicionIds, GestionRendicionFiltersDto scope)
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

            return await CorreosSolicitantesAsync(ctx, solicitudes);
        }

        public async Task<List<string>> GetCorreosTesoreria()
        {
            using var ctx = _factory.CreateDbContext();

            // El mismo requisito que abre la bandeja y que usa el envío
            // (GetTesoreriaCorreoInfo): el rol TESORERO. Ya no se pide además el puesto de
            // categoría Tesorero — si se pidiera, el aviso dejaría fuera a gente que sí entra.
            var rolTesorero = int.Parse(Roles.Tesorero);
            return await (
                from w   in ctx.Worker
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                join ur  in ctx.UserRole on per.UserId equals (int?)ur.UserId
                where ur.RoleId == rolTesorero && ur.State && ur.Active
                   && w.EmailCorporativo != null && w.EmailCorporativo != ""
                select w.EmailCorporativo!
            ).Distinct().ToListAsync();
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

        public async Task<string?> GetRendicionFolderUrl()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.GaRendicionFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.GaRendicionFolderId)
                .Select(f => f.LinkUrl)
                .FirstOrDefaultAsync();
        }

        public async Task<ReembolsoCorreoInfoDto?> GetReembolsoCorreoInfo(int solicitudId)
        {
            using var ctx = _factory.CreateDbContext();

            var head = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                join u in ctx.User on (per != null ? per.UserId : null) equals (int?)u.UserId into uGroup
                from u in uGroup.DefaultIfEmpty()
                join r in ctx.GaRendicion on s.RendicionId equals (int?)r.Id into rGroup
                from r in rGroup.DefaultIfEmpty()
                where s.Id == solicitudId
                select new
                {
                    s.Id, WorkerInternalId = w.Id,
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                    Trabajador = per != null ? (per.FullName ?? "Trabajador") : "Trabajador",
                    Email = u != null ? u.Email : null,
                    s.FechaSalida, s.EstadoReembolsoId, s.ObservacionReembolso, s.ReembolsoDecididoPorId,
                    s.RendicionId,
                    NumeroPlanilla = r != null ? r.NumeroPlanilla : null,
                }
            ).FirstOrDefaultAsync();

            if (head == null) return null;

            var trayectoIds = await ctx.GaSolicitudTrayecto
                .Where(t => t.SolicitudId == solicitudId)
                .Select(t => t.Id)
                .ToListAsync();

            var monto = trayectoIds.Count == 0
                ? 0m
                : await ctx.GaSolicitudCaptura
                    .Where(c => trayectoIds.Contains(c.TrayectoId))
                    .SumAsync(c => (decimal?)c.Monto) ?? 0m;

            string? decididoPor = null;
            if (head.ReembolsoDecididoPorId.HasValue)
            {
                decididoPor = await (
                    from w in ctx.Worker
                    join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                    where per.UserId == head.ReembolsoDecididoPorId.Value
                    select per.FullName
                ).FirstOrDefaultAsync();
            }

            var codigo = await ctx.GaSolicitudSalida
                .Where(s => s.Id == solicitudId)
                .Select(s => s.Codigo)
                .FirstOrDefaultAsync();

            return new ReembolsoCorreoInfoDto
            {
                SolicitudId          = head.Id,
                WorkerId             = head.WorkerInternalId,
                Codigo               = codigo ?? $"#{head.Id}",
                Trabajador           = head.Trabajador,
                SolicitanteEmail     = head.Email,
                Area                 = await ResolveAreaNombreAsync(ctx, head.AreaScopeId),
                FechaSalida          = head.FechaSalida,
                NumeroPlanilla       = PlanillaRendicionHelper.NumeroPlanilla(head.NumeroPlanilla),
                RendicionId          = head.RendicionId,
                TrayectosCount       = trayectoIds.Count,
                MontoTotal           = monto,
                EstadoReembolso      = EstadosSalida.Reembolso.Nombre(head.EstadoReembolsoId),
                ObservacionReembolso = head.ObservacionReembolso,
                DecididoPor          = decididoPor,
            };
        }

        public async Task<TesoreriaCorreoInfoDto?> GetTesoreriaCorreoInfo(int rendicionId)
        {
            using var ctx = _factory.CreateDbContext();

            var planilla = await ctx.GaRendicion
                .Where(r => r.Id == rendicionId)
                .Select(r => new { r.Id, r.Codigo, r.NumeroPlanilla, r.FirmadoPorId })
                .FirstOrDefaultAsync();
            if (planilla == null) return null;

            // Todas las salidas de la planilla, sin recorte de visibilidad: lo que Tesorería va a
            // pagar es el documento completo, no la parte que ve el revisor que firmó.
            var salidas = await (
                from s   in ctx.GaSolicitudSalida.Where(x => x.RendicionId == rendicionId)
                join w   in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                select new
                {
                    s.Id,
                    w.Subarea,
                    Trabajador = per != null ? (per.FullName ?? "Trabajador") : "Trabajador",
                    Area       = w.Area,
                    s.FechaSalida,
                }
            ).ToListAsync();
            if (salidas.Count == 0) return null;

            var solicitudIds = salidas.Select(s => s.Id).ToList();

            var trayectos = await ctx.GaSolicitudTrayecto
                .Where(t => solicitudIds.Contains(t.SolicitudId))
                .Select(t => new { t.Id, t.SolicitudId, t.LugarOrigenId, t.LugarDestinoId })
                .ToListAsync();

            var subareaPorSolicitud = salidas.ToDictionary(s => s.Id, s => s.Subarea);
            var importes = await ImporteRendidoLoader.LoadAsync(
                ctx,
                trayectos
                    .Select(t => new ImporteRendidoLoader.TrayectoParaImporte(
                        t.Id,
                        subareaPorSolicitud.TryGetValue(t.SolicitudId, out var sub) ? sub : null,
                        t.LugarOrigenId,
                        t.LugarDestinoId))
                    .ToList());

            var consolidado = (await ConsolidadoS10Loader.LoadPorRendicionAsync(
                ctx, new List<int> { rendicionId })).GetValueOrDefault(rendicionId);

            string? firmadoPor = null;
            if (planilla.FirmadoPorId.HasValue)
            {
                firmadoPor = await ctx.Person
                    .Where(p => p.UserId == planilla.FirmadoPorId.Value)
                    .Select(p => p.FullName)
                    .FirstOrDefaultAsync();
            }

            // El mismo requisito que abre la bandeja: el rol TESORERO. La categoría del puesto ya
            // no entra en la cuenta, así que el aviso llega a todos los que pueden trabajarlo.
            var rolTesorero = int.Parse(Roles.Tesorero);
            var destinatarios = await (
                from w   in ctx.Worker
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                join ur  in ctx.UserRole on per.UserId equals (int?)ur.UserId
                where ur.RoleId == rolTesorero && ur.State && ur.Active
                   && w.EmailCorporativo != null && w.EmailCorporativo != ""
                select w.EmailCorporativo!
            ).Distinct().ToListAsync();

            var nombres = salidas.Select(s => s.Trabajador).Distinct().ToList();
            var primera = salidas[0];

            return new TesoreriaCorreoInfoDto
            {
                Destinatarios = destinatarios,
                Datos = new ReembolsoPlanillaCorreoDatos
                {
                    RendicionId    = rendicionId,
                    Codigo         = PlanillaRendicionHelper.CodigoRendicion(planilla.Codigo, rendicionId),
                    // Una planilla puede agrupar a varios: se nombra al primero y se cuenta el resto.
                    Trabajador     = nombres.Count > 1 ? $"{nombres[0]} +{nombres.Count - 1}" : nombres[0],
                    Area           = nombres.Count > 1 ? null : primera.Area,
                    NumeroPlanilla = PlanillaRendicionHelper.NumeroPlanilla(planilla.NumeroPlanilla),
                    Periodo        = PlanillaRendicionHelper.EtiquetaPeriodo(
                                        salidas.Min(s => s.FechaSalida), salidas.Max(s => s.FechaSalida)),
                    SalidasCount   = salidas.Count,
                    MontoTotal     = trayectos.Sum(t => importes.TryGetValue(t.Id, out var imp) ? imp.Importe : 0m),
                    NumeroGuia     = consolidado?.NumeroGuia,
                    FirmadoPor     = firmadoPor,
                },
            };
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

        /// <summary>
        /// De los ids indicados, cuáles tienen un reembolso listo para decidir: rendidas, con
        /// Consolidado del S10 adjunto y todavía Pendiente o Rechazado.
        /// </summary>
        private static async Task<HashSet<int>> IdsConReembolsoRevisableAsync(AppDbContext ctx, List<int> ids)
        {
            if (ids.Count == 0) return new();

            var candidatas = await ctx.GaSolicitudSalida
                .Where(s => ids.Contains(s.Id)
                         && s.EstadoRendicionId == EstadosSalida.Rendicion.Rendido
                         && (s.EstadoReembolsoId == EstadosSalida.Reembolso.Pendiente
                          || s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado))
                .Select(s => new { s.Id, s.RendicionId })
                .ToListAsync();

            if (candidatas.Count == 0) return new();

            var consolidados = await ConsolidadoS10Loader.LoadAsync(
                ctx, candidatas.ToDictionary(x => x.Id, x => x.RendicionId));

            return candidatas.Where(x => consolidados.ContainsKey(x.Id)).Select(x => x.Id).ToHashSet();
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
            PlanillaRendicionLoader.PlanillaFila p, HashSet<int> misWorkerIdsQueNoDecido, HashSet<int> porDecidir) => new()
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
            RevisorNotificadoAt  = p.RevisorNotificadoAt,
            PorDecidirCount    = p.Salidas.Count(s => porDecidir.Contains(s.Id)),
            // Basta una salida suya que no le toque decidir para apagar la planilla entera: la
            // primera revisión es del documento completo, no se puede aprobar "a medias".
            PuedeDecidir       = !p.Salidas.Any(s => misWorkerIdsQueNoDecido.Contains(s.WorkerId)),
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
            d.ObservacionReembolso = o.ObservacionReembolso; d.RevisorNotificadoAt = o.RevisorNotificadoAt;
            d.PorDecidirCount = o.PorDecidirCount;
            d.PuedeDecidir = o.PuedeDecidir;
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
