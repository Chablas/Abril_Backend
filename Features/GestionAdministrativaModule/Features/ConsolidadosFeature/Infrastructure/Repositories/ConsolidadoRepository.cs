using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Consolidados.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Models;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Consolidadores.Interfaces;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Consolidados.Infrastructure.Repositories
{
    /// <summary>
    /// Los Consolidados del S10 desde los dos lados que trabajan sobre ellos: la jefatura que decide
    /// y firma el reembolso, y el consolidador que los adjuntó y los subsana.
    ///
    /// La fila NO se arma desde la planilla sino desde la SALIDA: el consolidado de cada salida se
    /// resuelve con la precedencia de <see cref="ConsolidadoS10Loader"/> (el propio de la salida si
    /// lo tiene —solo en registros antiguos—, y si no el de su planilla) y después se agrupan las
    /// salidas por documento. Es la misma precedencia con la que se decide si un reembolso está
    /// listo para revisar, así que ninguna salida decidible puede quedarse sin su fila acá.
    ///
    /// Ver no es decidir: la visibilidad (ámbito CONSOLIDADOS) da a ver el consolidado, pero su
    /// reembolso lo decide SOLO la jefatura de los trabajadores (<see cref="RevisorDeLaSalida"/>), y
    /// los trámites del consolidador SOLO quien es consolidador de todos sus trabajadores
    /// (<c>IConsolidadorResolver</c>).
    /// </summary>
    public class ConsolidadoRepository : IConsolidadoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IJefeRevisorResolver _jefeResolver;
        private readonly IConsolidadorResolver _consolidadorResolver;

        public ConsolidadoRepository(
            IDbContextFactory<AppDbContext> factory,
            IJefeRevisorResolver jefeResolver,
            IConsolidadorResolver consolidadorResolver)
        {
            _factory = factory;
            _jefeResolver = jefeResolver;
            _consolidadorResolver = consolidadorResolver;
        }

        // ══ Lectura ═════════════════════════════════════════════════════════

        public async Task<List<ConsolidadoListItemDto>> GetAll(ConsolidadoFiltersDto filters)
        {
            using var ctx = _factory.CreateDbContext();

            var armado = await ArmarAsync(ctx, SalidasVisibles(ctx, filters), filters.CurrentUserId);
            return Filtrar(armado.Items, filters);
        }

        public async Task<ConsolidadoDetalleDto?> GetDetalle(int consolidadoId, ConsolidadoFiltersDto scope)
        {
            using var ctx = _factory.CreateDbContext();

            var rendicionIds = await RendicionesCubiertasAsync(ctx, consolidadoId);
            if (rendicionIds.Count == 0) return null;

            var query = SalidasVisibles(ctx, SoloVisibilidad(scope))
                .Where(s => s.RendicionId != null && rendicionIds.Contains(s.RendicionId!.Value));

            var armado = await ArmarAsync(ctx, query, scope.CurrentUserId, conDetalle: true);

            var cabecera = armado.Items.FirstOrDefault(x => x.Id == consolidadoId);
            if (cabecera == null) return null;

            var detalle = new ConsolidadoDetalleDto();
            CopiarCabecera(cabecera, detalle);
            detalle.Salidas = armado.Salidas.TryGetValue(consolidadoId, out var salidas)
                ? salidas
                : new List<ConsolidadoSalidaDto>();
            return detalle;
        }

        public async Task<SolicitudSalidaDetalleDto?> GetSalidaDetalle(int solicitudId, ConsolidadoFiltersDto scope)
        {
            using var ctx = _factory.CreateDbContext();

            // Solo una salida rendida y dentro del alcance: la que se ve en el detalle del
            // consolidado. Mandar un id cualquiera no abre la salida de un área que no le compete.
            var visible = await SalidasVisibles(ctx, SoloVisibilidad(scope))
                .AnyAsync(s => s.Id == solicitudId && s.RendicionId != null);
            if (!visible) return null;

            return await SalidaDetalleLoader.LoadAsync(ctx, solicitudId, conAptitudParaRendir: false);
        }

        public async Task<ConsolidadoFilterDataDto> GetFilterData(ConsolidadoFiltersDto scope)
        {
            using var ctx = _factory.CreateDbContext();

            var seesAll = scope.SeesAll;
            var areaIds = scope.VisibleAreaScopeIds ?? new List<int>();
            var uid     = scope.CurrentUserId;

            // Trabajadores con al menos una salida ya rendida: los demás todavía no tienen planilla
            // y, por lo tanto, tampoco consolidado.
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

            // El periodo de un consolidado es el mes de su salida más antigua — el mismo criterio
            // que la tabla, si no el filtro dejaría fuera consolidados que sí muestra. Se agrupa
            // por documento (y no por planilla) porque uno puede cubrir varias.
            var salidas = await SalidasVisibles(ctx, SoloVisibilidad(scope))
                .Where(s => s.RendicionId != null)
                .Select(s => new { s.Id, s.RendicionId, s.FechaSalida })
                .ToListAsync();

            var consolidados = await ConsolidadoS10Loader.LoadAsync(
                ctx, salidas.ToDictionary(x => x.Id, x => x.RendicionId));

            var periodos = salidas
                .Where(x => consolidados.ContainsKey(x.Id))
                .GroupBy(x => consolidados[x.Id].Id)
                .Select(g => g.Min(x => x.FechaSalida))
                .Select(f => (f.Year, f.Month))
                .Distinct()
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .Select(p => new PeriodoConsolidadoOptionDto
                {
                    Anio  = p.Year,
                    Mes   = p.Month,
                    Label = PlanillaRendicionHelper.EtiquetaMes(p.Year, p.Month),
                })
                .ToList();

            return new ConsolidadoFilterDataDto
            {
                Trabajadores = trabajadores,
                AreaTree     = areaTree,
                Periodos     = periodos,
            };
        }

        // ══ Armado de las filas ═════════════════════════════════════════════

        /// <summary>
        /// Lo que devuelve <see cref="ArmarAsync"/>: las filas y, si se pidió con detalle, las
        /// salidas de cada consolidado. Van juntas porque ya se recorrieron para armar la cabecera:
        /// devolverlas aparte obligaría a volver a la base por lo mismo.
        /// </summary>
        private sealed record Armado(
            List<ConsolidadoListItemDto> Items,
            Dictionary<int, List<ConsolidadoSalidaDto>> Salidas);

        private async Task<Armado> ArmarAsync(
            AppDbContext ctx,
            IQueryable<GaSolicitudSalida> salidasVisibles,
            int? currentUserId,
            bool conDetalle = false)
        {
            var vacio = new Armado(new(), new());

            var planillas = await PlanillaRendicionLoader.LoadAsync(ctx, salidasVisibles, conDetalle);
            if (planillas.Count == 0) return vacio;

            // Consolidado de cada salida, con la precedencia del módulo. Las salidas sin consolidado
            // no tienen nada que mostrar acá: su planilla está en Gestión de Rendiciones esperando
            // que el consolidador se lo adjunte.
            var rendicionPorSolicitud = planillas
                .SelectMany(p => p.Salidas.Select(s => (SolicitudId: s.Id, RendicionId: (int?)p.Id)))
                .ToDictionary(x => x.SolicitudId, x => x.RendicionId);

            var consolidadoPorSolicitud = await ConsolidadoS10Loader.LoadAsync(ctx, rendicionPorSolicitud);
            if (consolidadoPorSolicitud.Count == 0) return vacio;

            var porDecidir = await IdsConReembolsoRevisableAsync(
                ctx, consolidadoPorSolicitud.Keys.ToList());

            // De lo que está por decidir, lo que le toca decidir a ESTE usuario: las salidas de los
            // trabajadores de los que es la jefatura. Un número fijo de consultas para toda la tabla.
            var decidibles = await DecidiblesPorUsuarioAsync(
                ctx,
                planillas.SelectMany(p => p.Salidas)
                    .Where(s => porDecidir.Contains(s.Id))
                    .Select(s => (s.Id, s.WorkerId))
                    .ToList(),
                currentUserId);

            // Un grupo por documento: sus planillas visibles y, dentro de cada una, las salidas que
            // ese documento cubre (en los consolidados por salida es solo una de ellas).
            var grupos = new Dictionary<int, List<(PlanillaRendicionLoader.PlanillaFila Planilla,
                                                  List<PlanillaRendicionLoader.SalidaFila> Salidas)>>();
            var dtoPorConsolidado = new Dictionary<int, ConsolidadoS10Dto>();

            foreach (var planilla in planillas)
            {
                foreach (var grupo in planilla.Salidas
                             .Where(s => consolidadoPorSolicitud.ContainsKey(s.Id))
                             .GroupBy(s => consolidadoPorSolicitud[s.Id].Id))
                {
                    dtoPorConsolidado[grupo.Key] = consolidadoPorSolicitud[grupo.First().Id];

                    if (!grupos.TryGetValue(grupo.Key, out var lista))
                        grupos[grupo.Key] = lista = new();

                    lista.Add((planilla, grupo.ToList()));
                }
            }

            if (grupos.Count == 0) return vacio;

            // Lo que hay que resolver mirando el documento entero y no solo lo visible: las planillas
            // que cubre pero el usuario no ve (su monto completo) y los trabajadores de todas ellas,
            // que son por los que hay que ser consolidador.
            var cubiertas = dtoPorConsolidado.Values
                .SelectMany(c => c.Rendiciones.Select(r => r.Id))
                .Concat(grupos.SelectMany(g => g.Value.Select(x => x.Planilla.Id)))
                .Distinct()
                .ToList();

            var enTabla     = planillas.ToDictionary(p => p.Id);
            var totalesFuera = await TotalPlanillaLoader.LoadAsync(
                ctx, cubiertas.Where(id => !enTabla.ContainsKey(id)).ToList());

            var agrupables = await ConsolidadoS10Agrupacion.LoadPlanillasAsync(ctx, cubiertas);

            var todosLosTrabajadores = agrupables.Values.SelectMany(a => a.WorkerIds).Distinct().ToList();
            var puedeConsolidarPor = currentUserId == null || todosLosTrabajadores.Count == 0
                ? new HashSet<int>()
                : await _consolidadorResolver.FiltrarQuePuedeConsolidarAsync(currentUserId.Value, todosLosTrabajadores);

            // La corrección con el ERP viva de cada planilla (casi ninguna la tiene).
            var correcciones = await CorreccionS10Loader.LoadVigentesAsync(ctx, cubiertas);

            // Quién adjuntó cada consolidado y bajo qué razón social: la del consolidador.
            var subidoPor = await SubidoPorAsync(ctx, grupos.Keys.ToList());
            var razones   = await RazonSocialConsolidador.LoadPorUsuarioAsync(
                ctx, subidoPor.Values.Select(x => x.UserId).Distinct().ToList());

            decimal MontoCompletoDe(int rendicionId) => enTabla.TryGetValue(rendicionId, out var fila)
                ? fila.MontoTotalPlanilla
                : totalesFuera.GetValueOrDefault(rendicionId);

            var salidasDelDetalle = new Dictionary<int, List<ConsolidadoSalidaDto>>();
            var items = new List<ConsolidadoListItemDto>(grupos.Count);

            foreach (var (consolidadoId, lista) in grupos)
            {
                var dto      = dtoPorConsolidado[consolidadoId];
                var visibles = lista.SelectMany(x => x.Salidas).ToList();

                // Los códigos del documento salen de sus vínculos vigentes; un consolidado por
                // salida no tiene ninguno, así que su única planilla es la de esa salida.
                var codigoCubierta = dto.Rendiciones.ToDictionary(r => r.Id, r => r.Codigo);
                foreach (var (planilla, _) in lista) codigoCubierta.TryAdd(planilla.Id, planilla.Codigo);

                var rendiciones = codigoCubierta
                    .Select(kv =>
                    {
                        var deLaTabla = lista.FirstOrDefault(x => x.Planilla.Id == kv.Key);
                        if (deLaTabla.Planilla == null)
                        {
                            return new ConsolidadoPlanillaDto
                            {
                                Id                 = kv.Key,
                                Codigo             = kv.Value,
                                Visible            = false,
                                MontoTotalPlanilla = MontoCompletoDe(kv.Key),
                            };
                        }

                        var p     = deLaTabla.Planilla;
                        var suyas = deLaTabla.Salidas;
                        return new ConsolidadoPlanillaDto
                        {
                            Id                 = p.Id,
                            Codigo             = p.Codigo,
                            Visible            = true,
                            MontoTotalPlanilla = p.MontoTotalPlanilla,
                            NumeroPlanilla     = p.NumeroPlanilla,
                            Periodo            = PlanillaRendicionHelper.EtiquetaPeriodo(
                                                     suyas.Min(s => s.FechaSalida),
                                                     suyas.Max(s => s.FechaSalida)),
                            Trabajadores       = suyas.Select(s => s.Trabajador).Distinct().ToList(),
                            SalidasCount       = suyas.Count,
                            MontoVisible       = suyas.Sum(s => s.Monto),
                            EstadoReembolso    = PlanillaRendicionHelper.ResumirEstadoReembolso(
                                                     suyas.Select(s => s.EstadoReembolsoId)),
                            PdfUrl             = p.PdfUrl,
                            PdfFilename        = p.PdfFilename,
                            PdfFirmadoUrl      = p.PdfFirmadoUrl,
                            PdfFirmadoFilename = p.PdfFirmadoFilename,
                            FirmadoAt          = p.FirmadoAt,
                        };
                    })
                    .OrderBy(r => r.Codigo, StringComparer.Ordinal)
                    .ToList();

                var desde = visibles.Min(s => s.FechaSalida);
                var hasta = visibles.Max(s => s.FechaSalida);

                // La observación vigente y su origen salen de la MISMA salida: un consolidado que
                // devolvió Tesorería no puede mostrar el motivo de una y el rótulo de otra.
                var observada = visibles.FirstOrDefault(
                    s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado
                      && !string.IsNullOrWhiteSpace(s.ObservacionReembolso));

                // El consolidado cubre los documentos enteros: el trámite es de quien puede
                // consolidar por TODOS sus trabajadores, también por los que la tabla no muestra.
                var trabajadoresDelDocumento = rendiciones
                    .SelectMany(r => agrupables.TryGetValue(r.Id, out var a) ? a.WorkerIds : new List<int>())
                    .Distinct()
                    .ToList();
                var puedeConsolidar = trabajadoresDelDocumento.Count > 0
                                   && trabajadoresDelDocumento.All(puedeConsolidarPor.Contains);

                var correccion = rendiciones
                    .Select(r => correcciones.GetValueOrDefault(r.Id))
                    .FirstOrDefault(c => c != null);

                subidoPor.TryGetValue(dto.Id, out var quienSubio);
                var razon = quienSubio.UserId > 0 ? razones.GetValueOrDefault(quienSubio.UserId) : null;

                items.Add(new ConsolidadoListItemDto
                {
                    Id              = dto.Id,
                    Codigo          = dto.Codigo,
                    NumeroReembolso = dto.NumeroReembolso,
                    PlanillaGrupalUrl      = dto.PlanillaGrupalUrl,
                    PlanillaGrupalFilename = dto.PlanillaGrupalFilename,
                    MontoTotal      = dto.MontoTotal,
                    MontoVisible    = visibles.Sum(s => s.Monto),

                    PdfUrl             = dto.PdfUrl,
                    PdfFilename        = dto.PdfFilename,
                    PdfFirmadoUrl      = dto.PdfFirmadoUrl,
                    PdfFirmadoFilename = dto.PdfFirmadoFilename,
                    FirmadoAt          = dto.FirmadoAt,
                    UploadedAt         = dto.UploadedAt,
                    SubidoPor          = quienSubio.Nombre,

                    Rendiciones   = rendiciones,
                    Trabajadores  = visibles.Select(s => s.Trabajador).Distinct().ToList(),
                    SalidasCount  = visibles.Count,
                    RazonSocialId = razon?.Id,
                    RazonSocial   = razon?.Nombre,

                    Periodo     = PlanillaRendicionHelper.EtiquetaPeriodo(desde, hasta),
                    PeriodoAnio = desde.Year,
                    PeriodoMes  = desde.Month,

                    EstadoReembolso = PlanillaRendicionHelper.ResumirEstadoReembolso(
                                          visibles.Select(s => s.EstadoReembolsoId)),
                    ReembolsoMixto  = visibles.Select(s => s.EstadoReembolsoId).Distinct().Count() > 1,
                    ObservacionReembolso = observada?.ObservacionReembolso,
                    ObservacionReembolsoOrigen = EstadosSalida.OrigenObservacionReembolso.Nombre(
                        observada?.ObservacionReembolsoOrigenId),

                    // Solo lo que el usuario decide: una jefatura con trabajadores en dos
                    // consolidados, o un consolidado con trabajadores de dos jefaturas, decide su
                    // parte (las firmas se acumulan sobre el mismo documento).
                    PorDecidirCount = visibles.Count(s => decidibles.Contains(s.Id)),

                    // Avisar tiene sentido si algo Pendiente espera a OTRA jefatura: un consolidador
                    // que además es el jefe de esos trabajadores lo decide él mismo.
                    PuedeConsolidar     = puedeConsolidar,
                    PuedeAvisarJefatura = puedeConsolidar
                                       && visibles.Any(s => porDecidir.Contains(s.Id)
                                                         && !decidibles.Contains(s.Id)
                                                         && s.EstadoReembolsoId == EstadosSalida.Reembolso.Pendiente),
                    JefaturaAvisadaAt   = visibles.Max(s => s.RevisorNotificadoAt),
                    // Solo desde una observación y una a la vez: mientras haya una viva, lo que
                    // sigue es esperarla o recargar el consolidado.
                    PuedeSolicitarCorreccion = puedeConsolidar
                                            && correccion == null
                                            && visibles.Any(s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado),
                    CorreccionS10 = correccion,
                });

                if (conDetalle)
                {
                    salidasDelDetalle[dto.Id] = lista
                        .SelectMany(x => x.Salidas.Select(s => new ConsolidadoSalidaDto
                        {
                            Id                   = s.Id,
                            Codigo               = s.Codigo,
                            RendicionId          = x.Planilla.Id,
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
                        }))
                        .ToList();
                }
            }

            // Lo último adjuntado primero: es lo que está esperando una decisión.
            return new Armado(
                items.OrderByDescending(x => x.UploadedAt).ThenByDescending(x => x.Id).ToList(),
                salidasDelDetalle);
        }

        /// <summary>
        /// Quién subió cada consolidado —el consolidador—: su usuario (para resolver su razón social)
        /// y su nombre (la columna «Adjuntado por»).
        /// </summary>
        private static async Task<Dictionary<int, (int UserId, string? Nombre)>> SubidoPorAsync(
            AppDbContext ctx, List<int> consolidadoIds)
        {
            if (consolidadoIds.Count == 0) return new();

            var filas = await (
                from c   in ctx.GaConsolidadoS10
                join per in ctx.Person on c.UploadedById equals per.UserId into perGroup
                from per in perGroup.DefaultIfEmpty()
                where consolidadoIds.Contains(c.Id)
                select new { c.Id, c.UploadedById, Nombre = per != null ? per.FullName : null }
            ).ToListAsync();

            return filas
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => (g.First().UploadedById, g.Select(x => x.Nombre).FirstOrDefault(n => n != null)));
        }

        /// <summary>
        /// Planillas que cubre un consolidado: sus vínculos vigentes y, en los registros antiguos
        /// atados a una sola salida, la planilla de esa salida.
        /// </summary>
        private static async Task<List<int>> RendicionesCubiertasAsync(AppDbContext ctx, int consolidadoId)
        {
            var ids = await ctx.GaConsolidadoS10Rendicion
                .Where(v => v.State && v.ConsolidadoS10Id == consolidadoId)
                .Select(v => v.RendicionId)
                .ToListAsync();

            var solicitudId = await ctx.GaConsolidadoS10
                .Where(c => c.Id == consolidadoId && c.State)
                .Select(c => c.SolicitudId)
                .FirstOrDefaultAsync();

            if (solicitudId != null)
            {
                var deLaSalida = await ctx.GaSolicitudSalida
                    .Where(s => s.Id == solicitudId.Value)
                    .Select(s => s.RendicionId)
                    .FirstOrDefaultAsync();
                if (deLaSalida != null) ids.Add(deLaSalida.Value);
            }

            return ids.Distinct().ToList();
        }

        // ══ Escritura: la decisión del reembolso ════════════════════════════

        public async Task<List<int>> ResolverSolicitudIds(
            IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope)
        {
            var ids = consolidadoIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            // Las planillas de esos consolidados, y de ellas solo las salidas que el usuario ve:
            // un consolidado puede cubrir áreas que no están en su alcance.
            var rendicionIds = await ctx.GaConsolidadoS10Rendicion
                .Where(v => v.State && ids.Contains(v.ConsolidadoS10Id))
                .Select(v => v.RendicionId)
                .Distinct()
                .ToListAsync();

            // Las salidas de esas planillas y, en los registros antiguos, las que tienen el
            // consolidado atado directamente (esas no dejan vínculo de planilla).
            var candidatas = await SalidasVisibles(ctx, SoloVisibilidad(scope))
                .Where(s => s.RendicionId != null
                         && (rendicionIds.Contains(s.RendicionId!.Value)
                          || ctx.GaConsolidadoS10.Any(
                                 c => c.State && c.SolicitudId == s.Id && ids.Contains(c.Id))))
                .Select(s => new { s.Id, s.RendicionId })
                .ToListAsync();

            if (candidatas.Count == 0) return new();

            // Y se descartan las salidas de esas planillas cuyo consolidado vigente es OTRO (pasa
            // solo con los registros antiguos): el documento que se decide es el seleccionado.
            var consolidados = await ConsolidadoS10Loader.LoadAsync(
                ctx, candidatas.ToDictionary(x => x.Id, x => x.RendicionId));

            return candidatas
                .Where(x => consolidados.TryGetValue(x.Id, out var c) && ids.Contains(c.Id))
                .Select(x => x.Id)
                .ToList();
        }

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
                // Esta pantalla es la de la jefatura; la de Tesorería marca el otro origen (RG-49).
                s.ObservacionReembolsoOrigenId = EstadosSalida.OrigenObservacionReembolso.Jefatura;
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
                .Select(c => new { c.Id, c.PdfUrl, c.PdfFilename, c.PdfFirmadoUrl })
                .ToDictionaryAsync(c => c.Id, c => c);

            // Un consolidado compartido puede llegar ya firmado por otro jefe, que aprobó otra de
            // sus planillas: la firma nueva se suma sobre esa copia, en el lugar siguiente, en vez
            // de pisarla. Y si quien aprueba ya lo firmó, no se vuelve a firmar.
            var firmantes = await FirmantesPorConsolidadoAsync(ctx, consolidadoIds);

            return planillas.Select(p =>
            {
                var solicitudIds = porRendicion[p.Id];

                var docs = solicitudIds
                    .Select(sid => consolidadoPorSolicitud.TryGetValue(sid, out var dto) ? dto.Id : (int?)null)
                    .Where(cid => cid != null)
                    .Select(cid => cid!.Value)
                    .Distinct()
                    .Where(cid => consolidados.ContainsKey(cid))
                    .Where(cid => !(firmantes.TryGetValue(cid, out var yaFirmaron) && yaFirmaron.Contains(reviewerUserId)))
                    .Select(cid =>
                    {
                        var c = consolidados[cid];
                        var previas = c.PdfFirmadoUrl != null && firmantes.TryGetValue(cid, out var yaFirmaron)
                            ? yaFirmaron.Count
                            : 0;
                        return new DocumentoParaFirmarDto
                        {
                            Id       = cid,
                            Url      = previas > 0 ? c.PdfFirmadoUrl! : c.PdfUrl,
                            Filename = c.PdfFilename,
                            Slot     = previas,
                        };
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
                // Al aprobar se limpia la observación y de quién era: ya no hay nada que subsanar,
                // y dejar el origen puesto haría que Tesorería siguiera viendo como "suya" una
                // planilla que ya volvió firmada.
                s.ObservacionReembolso         = null;
                s.ObservacionReembolsoOrigenId = null;
            }

            await ctx.SaveChangesAsync();
            return solicitudes.Select(s => s.Id).ToList();
        }

        /// <summary>
        /// Las salidas de la selección cuyo reembolso el usuario puede decidir hoy: elegibles
        /// (rendidas, con Consolidado del S10 y sin decidir) y de trabajadores de los que es la
        /// jefatura. Lanza 400 si no hay nada por decidir y 403 si hay pero no le toca a él; la
        /// comparten la aprobación y la observación para que las dos apliquen exactamente la misma
        /// regla.
        ///
        /// Las salidas de otra jefatura que vengan en la selección se ignoran en silencio: un
        /// consolidado puede cubrir trabajadores de varias, y cada una decide (y firma) su parte.
        /// </summary>
        private async Task<List<GaSolicitudSalida>> SalidasDecidiblesAsync(
            AppDbContext ctx, List<int> idsList, int reviewerUserId)
        {
            var elegibles = await IdsConReembolsoRevisableAsync(ctx, idsList);
            if (elegibles.Count == 0)
                throw new AbrilException(await MotivoSinNadaQueDecidirAsync(ctx, idsList), 400);

            var solicitudes = await ctx.GaSolicitudSalida
                .Where(s => elegibles.Contains(s.Id))
                .ToListAsync();

            var decidibles = await DecidiblesPorUsuarioAsync(
                ctx, solicitudes.Select(s => (s.Id, s.WorkerId)).ToList(), reviewerUserId);

            if (decidibles.Count == 0)
                throw new AbrilException(
                    "Solo la jefatura de los trabajadores puede aprobar u observar el reembolso de este consolidado.",
                    403);

            return solicitudes.Where(s => decidibles.Contains(s.Id)).ToList();
        }

        /// <summary>
        /// Por qué no hay nada que decidir. El caso que de verdad pasa es el observado: el documento
        /// está en manos del consolidador y decirlo así evita que la jefatura crea que se rompió
        /// algo. El resto cae en el mensaje genérico.
        /// </summary>
        private static async Task<string> MotivoSinNadaQueDecidirAsync(AppDbContext ctx, List<int> idsList)
        {
            var observadas = await ctx.GaSolicitudSalida
                .CountAsync(s => idsList.Contains(s.Id)
                              && s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado);

            return observadas > 0
                ? "Este consolidado está observado: vuelve a la jefatura recién cuando el consolidador "
                  + "adjunte el Consolidado del S10 corregido."
                : "Ninguna de las salidas del consolidado tiene un reembolso por decidir.";
        }

        /// <summary>
        /// De las salidas dadas, las que decide el usuario: aquellas cuyo revisor resuelto (el mismo
        /// que recibe los correos del flujo) es él. Ver <see cref="RevisorDeLaSalida.EsElRevisor"/>.
        /// Un número fijo de consultas: las fichas del usuario, un lote al resolver y, solo si algún
        /// revisor es el fallback de GTH, el árbol de áreas.
        /// </summary>
        private async Task<HashSet<int>> DecidiblesPorUsuarioAsync(
            AppDbContext ctx, IReadOnlyCollection<(int Id, int WorkerId)> salidas, int? userId)
        {
            if (!userId.HasValue || salidas.Count == 0) return new();

            var quien = await RevisorDeLaSalida.CargarQuienDecideAsync(ctx, userId);
            if (quien.WorkerIds.Count == 0) return new();

            var revisores = await _jefeResolver.ResolveManyAsync(
                salidas.Select(s => s.WorkerId).Distinct().ToList());

            var necesitaArbol = salidas.Any(s => !revisores.TryGetValue(s.WorkerId, out var r) || r.WorkerId == null);
            var arbol = necesitaArbol
                ? await RevisorDeLaSalida.CargarArbolAsync(ctx)
                : new Dictionary<int, (int? Padre, string Nombre)>();

            return salidas
                .Where(s => RevisorDeLaSalida.EsElRevisor(quien, revisores.GetValueOrDefault(s.WorkerId), arbol))
                .Select(s => s.Id)
                .ToHashSet();
        }

        // ══ Correos ═════════════════════════════════════════════════════════

        public async Task<List<string>> GetCorreosConsolidadorPorDecidir(
            IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope, int userId)
        {
            // El recorte por visibilidad y por jefatura es el mismo que hace la escritura: el
            // preview no puede anunciar un correo por un consolidado que el usuario no decide.
            var visibles = await ResolverSolicitudIds(consolidadoIds, scope);
            if (visibles.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            var porDecidir = await IdsConReembolsoRevisableAsync(ctx, visibles);
            if (porDecidir.Count == 0) return new();

            var salidas = await ctx.GaSolicitudSalida
                .Where(s => porDecidir.Contains(s.Id))
                .Select(s => new { s.Id, s.WorkerId })
                .ToListAsync();

            var decidibles = await DecidiblesPorUsuarioAsync(
                ctx, salidas.Select(s => (s.Id, s.WorkerId)).ToList(), userId);
            if (decidibles.Count == 0) return new();

            return (await ConsolidadoCorreoLoader.LoadAsync(ctx, decidibles))
                .Select(d => d.ConsolidadorEmail)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<List<string>> GetCorreosTesoreria()
        {
            using var ctx = _factory.CreateDbContext();

            // El mismo requisito que abre la bandeja y que usa el envío
            // (GetTesoreriaCorreoInfo): el rol TESORERO. No se pide además el puesto de categoría
            // Tesorero — si se pidiera, el aviso dejaría fuera a gente que sí entra.
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

        public async Task<string?> GetRendicionFolderUrl()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.GaRendicionFolder
                .Where(f => f.State && f.Active)
                .OrderBy(f => f.GaRendicionFolderId)
                .Select(f => f.LinkUrl)
                .FirstOrDefaultAsync();
        }

        public async Task<List<ConsolidadoCorreoDatos>> GetConsolidadoCorreoDatos(IReadOnlyCollection<int> solicitudIds)
        {
            using var ctx = _factory.CreateDbContext();
            return await ConsolidadoCorreoLoader.LoadAsync(ctx, solicitudIds);
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
                ConsolidadoId = consolidado?.Id,
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
                    NumeroReembolso = consolidado?.NumeroReembolso,
                    FirmadoPor     = firmadoPor,
                },
            };
        }

        // ══ Trámites del consolidador ═══════════════════════════════════════

        public async Task<AvisoJefaturaInfoDto?> GetAvisoJefatura(
            int consolidadoId, ConsolidadoFiltersDto scope, int userId)
        {
            var visibles = await ResolverSolicitudIds(new[] { consolidadoId }, scope);
            if (visibles.Count == 0) return null;

            using var ctx = _factory.CreateDbContext();

            var info = new AvisoJefaturaInfoDto
            {
                PuedeConsolidar = await PuedeConsolidarAsync(ctx, consolidadoId, userId),
            };

            // Lo que está esperando a la jefatura: rendido, con consolidado y todavía Pendiente. Lo
            // Observado no: ahí la pelota está en el consolidador.
            var revisables = await IdsConReembolsoRevisableAsync(ctx, visibles);
            var pendientes = await ctx.GaSolicitudSalida
                .Where(s => revisables.Contains(s.Id) && s.EstadoReembolsoId == EstadosSalida.Reembolso.Pendiente)
                .Select(s => new { s.Id, s.WorkerId })
                .ToListAsync();
            if (pendientes.Count == 0) return info;

            info.SolicitudIds = pendientes.Select(p => p.Id).ToList();

            // La jefatura de esas salidas: el MISMO revisor que las decide en esta pantalla.
            var jefaturas = (await _jefeResolver.ResolveManyAsync(
                    pendientes.Select(p => p.WorkerId).Distinct().ToList()))
                .Values
                .Where(r => !string.IsNullOrWhiteSpace(r.Email))
                .ToList();

            info.JefaturaEmails = jefaturas
                .Select(r => r.Email.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            info.JefaturaNombres = jefaturas
                .Select(r => string.IsNullOrWhiteSpace(r.Nombre) ? r.Email.Trim() : r.Nombre!)
                .Distinct()
                .ToList();

            info.Datos = (await ConsolidadoCorreoLoader.LoadAsync(ctx, info.SolicitudIds))
                .FirstOrDefault(d => d.ConsolidadoId == consolidadoId);

            return info;
        }

        public async Task MarcarJefaturaAvisada(IReadOnlyCollection<int> solicitudIds, int userId)
        {
            var ids = solicitudIds.Distinct().ToList();
            if (ids.Count == 0) return;

            using var ctx = _factory.CreateDbContext();

            var now = DateTimeOffset.UtcNow;
            var salidas = await ctx.GaSolicitudSalida
                .Where(s => ids.Contains(s.Id))
                .ToListAsync();

            foreach (var s in salidas)
            {
                s.RevisorNotificadoAt    = now;
                s.RevisorNotificadoPorId = userId;
                s.UpdatedAt              = now;
            }
            await ctx.SaveChangesAsync();
        }

        public async Task<CorreccionConsolidadoPlanDto?> GetCorreccionPlan(
            int consolidadoId, ConsolidadoFiltersDto scope, int userId)
        {
            var visibles = await ResolverSolicitudIds(new[] { consolidadoId }, scope);
            if (visibles.Count == 0) return null;

            using var ctx = _factory.CreateDbContext();

            var consolidado = await ctx.GaConsolidadoS10
                .Where(c => c.Id == consolidadoId && c.State)
                .Select(c => new { c.Id, c.NumeroReembolso })
                .FirstOrDefaultAsync();
            if (consolidado == null) return null;

            var cubiertas = await RendicionesCubiertasAsync(ctx, consolidadoId);

            var observadas = await ctx.GaSolicitudSalida
                .Where(s => s.RendicionId != null && cubiertas.Contains(s.RendicionId.Value)
                         && s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado)
                .Select(s => new { s.Id, RendicionId = s.RendicionId!.Value })
                .ToListAsync();

            var plan = new CorreccionConsolidadoPlanDto
            {
                PuedeConsolidar        = await PuedeConsolidarAsync(ctx, consolidadoId, userId),
                RendicionIdsObservadas = observadas.Select(o => o.RendicionId).Distinct().ToList(),
                HayCorreccionEnCurso   = await ctx.GaCorreccionS10
                                            .AnyAsync(c => c.State && cubiertas.Contains(c.RendicionId)),
                NumeroReembolso        = consolidado.NumeroReembolso,
                Solicitante            = await ctx.Person
                                            .Where(p => p.UserId == userId && p.FullName != null)
                                            .Select(p => p.FullName)
                                            .FirstOrDefaultAsync(),
            };

            if (observadas.Count > 0)
                plan.Datos = (await ConsolidadoCorreoLoader.LoadAsync(ctx, observadas.Select(o => o.Id).ToList()))
                    .FirstOrDefault(d => d.ConsolidadoId == consolidadoId);

            return plan;
        }

        public async Task<List<GaCorreccionS10>> CrearCorrecciones(
            int consolidadoId, string? numeroReembolso, IReadOnlyCollection<int> rendicionIds,
            string motivo, int userId)
        {
            var ids = rendicionIds.Distinct().ToList();
            var texto = (motivo ?? string.Empty).Trim();

            using var ctx = _factory.CreateDbContext();

            // Se relee en la escritura: entre el plan y el correo pudo recargarse el consolidado o
            // pedirse otra corrección desde otra sesión.
            var observadas = await ctx.GaSolicitudSalida
                .Where(s => s.RendicionId != null && ids.Contains(s.RendicionId.Value)
                         && s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado)
                .Select(s => new
                {
                    RendicionId = s.RendicionId!.Value,
                    s.ObservacionReembolso,
                    s.ObservacionReembolsoOrigenId,
                })
                .ToListAsync();

            var conCorreccion = (await ctx.GaCorreccionS10
                    .Where(c => c.State && ids.Contains(c.RendicionId))
                    .Select(c => c.RendicionId)
                    .ToListAsync())
                .ToHashSet();

            var now = DateTimeOffset.UtcNow;

            var nuevas = observadas
                .GroupBy(o => o.RendicionId)
                .Where(g => !conCorreccion.Contains(g.Key))
                .Select(g =>
                {
                    // La observación y su origen salen de la MISMA salida, o el ERP leería un motivo
                    // con el rótulo del otro (jefatura / Tesorería).
                    var observada = g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ObservacionReembolso)) ?? g.First();
                    return new GaCorreccionS10
                    {
                        RendicionId      = g.Key,
                        ConsolidadoS10Id = consolidadoId,
                        Motivo           = texto,
                        MotivoJefatura   = observada.ObservacionReembolso,
                        MotivoOrigenId   = observada.ObservacionReembolsoOrigenId,
                        NumeroReembolso  = numeroReembolso,
                        EstadoId         = EstadosSalida.CorreccionS10.Solicitada,
                        SolicitadaPorId  = userId,
                        SolicitadaAt     = now,
                        State            = true,
                        CreatedDateTime  = now,
                    };
                })
                .ToList();

            if (nuevas.Count == 0)
                throw new AbrilException(
                    "El consolidado ya no tiene rendiciones observadas sin una corrección en curso.", 409);

            ctx.GaCorreccionS10.AddRange(nuevas);
            await ctx.SaveChangesAsync();
            return nuevas;
        }

        public async Task<List<string>> GetCorreosCoordinadorErp()
        {
            using var ctx = _factory.CreateDbContext();

            // Por ROL, igual que el aviso a Tesorería: el responsable ERP no cuelga del organigrama,
            // así que no hay área desde la que resolverlo.
            var rolErp = int.Parse(Roles.CoordinadorErp);
            return await (
                from w   in ctx.Worker
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                join ur  in ctx.UserRole on per.UserId equals (int?)ur.UserId
                where ur.RoleId == rolErp && ur.State && ur.Active
                   && w.EmailCorporativo != null && w.EmailCorporativo != ""
                select w.EmailCorporativo!
            ).Distinct().ToListAsync();
        }

        /// <summary>
        /// True si el usuario es consolidador de TODOS los trabajadores de las planillas que cubre el
        /// consolidado (sin recorte de visibilidad: el documento es uno solo). Mismo criterio que
        /// habilita subirlo en Gestión de Rendiciones.
        /// </summary>
        private async Task<bool> PuedeConsolidarAsync(AppDbContext ctx, int consolidadoId, int userId)
        {
            var cubiertas = await RendicionesCubiertasAsync(ctx, consolidadoId);
            var trabajadores = (await ConsolidadoS10Agrupacion.LoadPlanillasAsync(ctx, cubiertas))
                .Values.SelectMany(a => a.WorkerIds).Distinct().ToList();
            if (trabajadores.Count == 0) return false;

            var habilitado = await _consolidadorResolver.FiltrarQuePuedeConsolidarAsync(userId, trabajadores);
            return trabajadores.All(habilitado.Contains);
        }

        // ══ Helpers ═════════════════════════════════════════════════════════

        /// <summary>Salidas del alcance del usuario con los filtros de la pantalla ya aplicados.</summary>
        private static IQueryable<GaSolicitudSalida> SalidasVisibles(
            AppDbContext ctx, ConsolidadoFiltersDto filters)
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
        private static ConsolidadoFiltersDto SoloVisibilidad(ConsolidadoFiltersDto scope) => new()
        {
            CurrentUserId       = scope.CurrentUserId,
            SeesAll             = scope.SeesAll,
            VisibleAreaScopeIds = scope.VisibleAreaScopeIds,
        };

        /// <summary>
        /// De los ids indicados, cuáles tienen un reembolso listo para decidir: rendidas, con
        /// Consolidado del S10 adjunto y todavía PENDIENTE.
        ///
        /// Lo Observado NO entra, y eso es la regla, no un descuido: observar devuelve el documento
        /// al consolidador, y lo único que lo trae de vuelta es recargar el Consolidado del S10
        /// corregido (RG-23), que es lo que lo pone otra vez en Pendiente. Mientras eso no pase, la
        /// jefatura no puede aprobar por encima de su propia observación: firmaría el mismo papel
        /// que acaba de decir que estaba mal, y la segunda revisión existe justamente para cuadrar
        /// el importe del S10 contra lo rendido (RG-31). Vale igual para lo que devuelve Tesorería
        /// (RG-49): también vuelve al consolidador.
        /// </summary>
        private static async Task<HashSet<int>> IdsConReembolsoRevisableAsync(
            AppDbContext ctx, List<int> ids)
        {
            if (ids.Count == 0) return new();

            var candidatas = await ctx.GaSolicitudSalida
                .Where(s => ids.Contains(s.Id)
                         && s.EstadoRendicionId == EstadosSalida.Rendicion.Rendido
                         && s.EstadoReembolsoId == EstadosSalida.Reembolso.Pendiente)
                .Select(s => new { s.Id, s.RendicionId })
                .ToListAsync();

            if (candidatas.Count == 0) return new();

            var consolidados = await ConsolidadoS10Loader.LoadAsync(
                ctx, candidatas.ToDictionary(x => x.Id, x => x.RendicionId));

            return candidatas.Where(x => consolidados.ContainsKey(x.Id)).Select(x => x.Id).ToHashSet();
        }

        /// <summary>
        /// Quiénes firmaron ya cada consolidado: los jefes que aprobaron alguna de las planillas que
        /// cubre. Aprobar firma la planilla y su consolidado en el mismo acto, así que los firmantes
        /// del consolidado son los de sus planillas firmadas.
        /// </summary>
        private static async Task<Dictionary<int, HashSet<int>>> FirmantesPorConsolidadoAsync(
            AppDbContext ctx, List<int> consolidadoIds)
        {
            if (consolidadoIds.Count == 0) return new();

            var filas = await (
                from v in ctx.GaConsolidadoS10Rendicion
                join r in ctx.GaRendicion on v.RendicionId equals r.Id
                where v.State && consolidadoIds.Contains(v.ConsolidadoS10Id)
                   && r.FirmadoPorId != null && r.PdfFirmadoUrl != null
                select new { v.ConsolidadoS10Id, FirmadoPorId = r.FirmadoPorId!.Value }
            ).ToListAsync();

            return filas
                .GroupBy(f => f.ConsolidadoS10Id)
                .ToDictionary(g => g.Key, g => g.Select(f => f.FirmadoPorId).ToHashSet());
        }

        private static void CopiarCabecera(ConsolidadoListItemDto o, ConsolidadoDetalleDto d)
        {
            d.Id = o.Id; d.Codigo = o.Codigo;
            d.NumeroReembolso = o.NumeroReembolso; d.MontoTotal = o.MontoTotal;
            d.MontoVisible = o.MontoVisible;
            d.PdfUrl = o.PdfUrl; d.PdfFilename = o.PdfFilename;
            d.PlanillaGrupalUrl = o.PlanillaGrupalUrl; d.PlanillaGrupalFilename = o.PlanillaGrupalFilename;
            d.PdfFirmadoUrl = o.PdfFirmadoUrl; d.PdfFirmadoFilename = o.PdfFirmadoFilename;
            d.FirmadoAt = o.FirmadoAt; d.UploadedAt = o.UploadedAt; d.SubidoPor = o.SubidoPor;
            d.Rendiciones = o.Rendiciones; d.Trabajadores = o.Trabajadores; d.SalidasCount = o.SalidasCount;
            d.RazonSocialId = o.RazonSocialId; d.RazonSocial = o.RazonSocial;
            d.Periodo = o.Periodo; d.PeriodoAnio = o.PeriodoAnio; d.PeriodoMes = o.PeriodoMes;
            d.EstadoReembolso = o.EstadoReembolso; d.ReembolsoMixto = o.ReembolsoMixto;
            d.ObservacionReembolso = o.ObservacionReembolso;
            d.ObservacionReembolsoOrigen = o.ObservacionReembolsoOrigen;
            d.PorDecidirCount = o.PorDecidirCount;
            d.PuedeConsolidar = o.PuedeConsolidar; d.PuedeAvisarJefatura = o.PuedeAvisarJefatura;
            d.JefaturaAvisadaAt = o.JefaturaAvisadaAt; d.PuedeSolicitarCorreccion = o.PuedeSolicitarCorreccion;
            d.CorreccionS10 = o.CorreccionS10;
        }

        /// <summary>
        /// Filtros que se resuelven sobre la fila ya armada (el estado y el periodo de un
        /// consolidado salen de sus salidas, así que no se pueden pedir en la consulta).
        /// </summary>
        private static List<ConsolidadoListItemDto> Filtrar(
            List<ConsolidadoListItemDto> items, ConsolidadoFiltersDto filters)
        {
            IEnumerable<ConsolidadoListItemDto> q = items;

            if (!string.IsNullOrWhiteSpace(filters.EstadoReembolso))
                q = q.Where(x => x.EstadoReembolso == filters.EstadoReembolso!.Trim());

            if (filters.PeriodoAnio.HasValue && filters.PeriodoMes.HasValue)
                q = q.Where(x => x.PeriodoAnio == filters.PeriodoAnio.Value
                              && x.PeriodoMes  == filters.PeriodoMes.Value);

            if (!string.IsNullOrWhiteSpace(filters.Texto))
            {
                var texto = filters.Texto!.Trim();
                q = q.Where(x =>
                    (x.Codigo ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase)
                    || (x.NumeroReembolso ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase)
                    || x.Rendiciones.Any(r => r.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase)));
            }

            return q.ToList();
        }
    }
}
