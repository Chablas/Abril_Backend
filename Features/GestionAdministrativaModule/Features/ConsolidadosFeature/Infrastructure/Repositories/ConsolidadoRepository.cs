using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Consolidados.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Email;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Services.Revisores.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Consolidados.Infrastructure.Repositories
{
    /// <summary>
    /// Los Consolidados del S10 desde el lado de la jefatura que los firma.
    ///
    /// La fila NO se arma desde la planilla sino desde la SALIDA: el consolidado de cada salida se
    /// resuelve con la precedencia de <see cref="ConsolidadoS10Loader"/> (el propio de la salida si
    /// lo tiene —solo en registros antiguos—, y si no el de su planilla) y después se agrupan las
    /// salidas por documento. Es la misma precedencia con la que se decide si un reembolso está
    /// listo para revisar, así que ninguna salida decidible puede quedarse sin su fila acá.
    /// </summary>
    public class ConsolidadoRepository : IConsolidadoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IJefeRevisorResolver _jefeResolver;

        public ConsolidadoRepository(
            IDbContextFactory<AppDbContext> factory, IJefeRevisorResolver jefeResolver)
        {
            _factory = factory;
            _jefeResolver = jefeResolver;
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
            // que alguien se lo adjunte.
            var rendicionPorSolicitud = planillas
                .SelectMany(p => p.Salidas.Select(s => (SolicitudId: s.Id, RendicionId: (int?)p.Id)))
                .ToDictionary(x => x.SolicitudId, x => x.RendicionId);

            var consolidadoPorSolicitud = await ConsolidadoS10Loader.LoadAsync(ctx, rendicionPorSolicitud);
            if (consolidadoPorSolicitud.Count == 0) return vacio;

            var porDecidir = await IdsConReembolsoRevisableAsync(
                ctx, consolidadoPorSolicitud.Keys.ToList());
            var ajenas = await MisWorkerIdsQueNoDecidoAsync(ctx, currentUserId);

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
            // que cubre pero el usuario no ve (su monto completo) y la razón social de todos sus
            // trabajadores.
            var cubiertas = dtoPorConsolidado.Values
                .SelectMany(c => c.Rendiciones.Select(r => r.Id))
                .Concat(grupos.SelectMany(g => g.Value.Select(x => x.Planilla.Id)))
                .Distinct()
                .ToList();

            var enTabla     = planillas.ToDictionary(p => p.Id);
            var totalesFuera = await TotalPlanillaLoader.LoadAsync(
                ctx, cubiertas.Where(id => !enTabla.ContainsKey(id)).ToList());

            var agrupables = await ConsolidadoS10Agrupacion.LoadPlanillasAsync(ctx, cubiertas);
            var razones    = await ConsolidadoS10Agrupacion.LoadRazonSocialAsync(
                ctx, agrupables.Values.SelectMany(a => a.WorkerIds).Distinct().ToList());

            var subidoPor = await SubidoPorAsync(ctx, grupos.Keys.ToList());

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

                var trabajadoresDelDocumento = rendiciones
                    .SelectMany(r => agrupables.TryGetValue(r.Id, out var a) ? a.WorkerIds : new List<int>())
                    .Distinct()
                    .ToList();
                var razon = ConsolidadoS10Agrupacion.RazonSocialComun(trabajadoresDelDocumento, razones);

                items.Add(new ConsolidadoListItemDto
                {
                    Id              = dto.Id,
                    NumeroReembolso = dto.NumeroReembolso,
                    MontoTotal      = dto.MontoTotal,
                    MontoVisible    = visibles.Sum(s => s.Monto),

                    PdfUrl             = dto.PdfUrl,
                    PdfFilename        = dto.PdfFilename,
                    PdfFirmadoUrl      = dto.PdfFirmadoUrl,
                    PdfFirmadoFilename = dto.PdfFirmadoFilename,
                    FirmadoAt          = dto.FirmadoAt,
                    UploadedAt         = dto.UploadedAt,
                    SubidoPor          = subidoPor.GetValueOrDefault(dto.Id),

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

                    PorDecidirCount = visibles.Count(s => porDecidir.Contains(s.Id)),
                    // Basta una salida suya que no le toque decidir para apagar el documento entero:
                    // el reembolso se decide completo, no se puede aprobar "a medias".
                    PuedeDecidir = !visibles.Any(s => ajenas.Contains(s.WorkerId)),
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

        /// <summary>Nombre de quien subió cada consolidado, para la columna «Adjuntado por».</summary>
        private static async Task<Dictionary<int, string?>> SubidoPorAsync(
            AppDbContext ctx, List<int> consolidadoIds)
        {
            if (consolidadoIds.Count == 0) return new();

            return await (
                from c   in ctx.GaConsolidadoS10
                join per in ctx.Person on c.UploadedById equals per.UserId into perGroup
                from per in perGroup.DefaultIfEmpty()
                where consolidadoIds.Contains(c.Id)
                select new { c.Id, Nombre = per != null ? per.FullName : null }
            ).ToDictionaryAsync(x => x.Id, x => x.Nombre);
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
        /// (rendidas, con Consolidado del S10 y sin decidir) y ninguna suya que no le toque.
        /// Lanza 400/403 con el mismo mensaje que veía la pantalla; la comparten la aprobación y la
        /// observación para que las dos apliquen exactamente la misma regla.
        /// </summary>
        private async Task<List<GaSolicitudSalida>> SalidasDecidiblesAsync(
            AppDbContext ctx, List<int> idsList, int reviewerUserId)
        {
            var elegibles = await IdsConReembolsoRevisableAsync(ctx, idsList);
            if (elegibles.Count == 0)
                throw new AbrilException(
                    "Ninguna de las salidas del consolidado tiene un reembolso por decidir.", 400);

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

        // ══ Correos ═════════════════════════════════════════════════════════

        public async Task<List<string>> GetCorreosSolicitantesPorDecidir(
            IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope)
        {
            // El recorte por visibilidad es el mismo que hace la escritura: sin esto, el preview de
            // un consolidado delataría los correos de trabajadores de áreas que el usuario no ve.
            var visibles = await ResolverSolicitudIds(consolidadoIds, scope);
            if (visibles.Count == 0) return new();

            using var ctx = _factory.CreateDbContext();

            var porDecidir = await IdsConReembolsoRevisableAsync(ctx, visibles);
            if (porDecidir.Count == 0) return new();

            return await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                join u in ctx.User on (int?)per.UserId equals (int?)u.UserId
                where porDecidir.Contains(s.Id) && u.Email != null && u.Email != ""
                select u.Email!
            ).Distinct().ToListAsync();
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
                    s.Codigo,
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

            return new ReembolsoCorreoInfoDto
            {
                SolicitudId          = head.Id,
                WorkerId             = head.WorkerInternalId,
                Codigo               = head.Codigo ?? $"#{head.Id}",
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
                    NumeroReembolso = consolidado?.NumeroReembolso,
                    FirmadoPor     = firmadoPor,
                },
            };
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
        /// Se pregunta al MISMO resolver que decide a quién se le manda el correo de la revisión,
        /// así que en la web decide exactamente quien recibe ese correo.
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
        /// Consolidado del S10 adjunto y todavía Pendiente u Observado.
        /// </summary>
        private static async Task<HashSet<int>> IdsConReembolsoRevisableAsync(
            AppDbContext ctx, List<int> ids)
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

        private static void CopiarCabecera(ConsolidadoListItemDto o, ConsolidadoDetalleDto d)
        {
            d.Id = o.Id; d.NumeroReembolso = o.NumeroReembolso; d.MontoTotal = o.MontoTotal;
            d.MontoVisible = o.MontoVisible;
            d.PdfUrl = o.PdfUrl; d.PdfFilename = o.PdfFilename;
            d.PdfFirmadoUrl = o.PdfFirmadoUrl; d.PdfFirmadoFilename = o.PdfFirmadoFilename;
            d.FirmadoAt = o.FirmadoAt; d.UploadedAt = o.UploadedAt; d.SubidoPor = o.SubidoPor;
            d.Rendiciones = o.Rendiciones; d.Trabajadores = o.Trabajadores; d.SalidasCount = o.SalidasCount;
            d.RazonSocialId = o.RazonSocialId; d.RazonSocial = o.RazonSocial;
            d.Periodo = o.Periodo; d.PeriodoAnio = o.PeriodoAnio; d.PeriodoMes = o.PeriodoMes;
            d.EstadoReembolso = o.EstadoReembolso; d.ReembolsoMixto = o.ReembolsoMixto;
            d.ObservacionReembolso = o.ObservacionReembolso;
            d.ObservacionReembolsoOrigen = o.ObservacionReembolsoOrigen;
            d.PorDecidirCount = o.PorDecidirCount; d.PuedeDecidir = o.PuedeDecidir;
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
                    (x.NumeroReembolso ?? string.Empty).Contains(texto, StringComparison.OrdinalIgnoreCase)
                    || x.Rendiciones.Any(r => r.Codigo.Contains(texto, StringComparison.OrdinalIgnoreCase)));
            }

            return q.ToList();
        }
    }
}
