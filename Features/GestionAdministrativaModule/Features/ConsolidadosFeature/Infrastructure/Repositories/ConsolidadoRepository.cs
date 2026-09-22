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

            // El pipeline sale de la cabecera ya armada —no de las filas crudas— para que diga
            // exactamente lo mismo que los badges de arriba del modal. Todo lo anterior al paso del
            // consolidado va cumplido por definición: solo se consolida lo rendido y aprobado en la
            // primera revisión, así que el documento no podría existir de otra forma.
            detalle.Pipeline = ReembolsoPipelineBuilder.Build(new ReembolsoPipelineInput
            {
                Tipo                    = ReembolsoPipelineItem.Consolidado,
                Codigo                  = cabecera.Codigo,
                Rendida                 = true,
                SustentosCompletos      = true,
                EstadoPrimeraRevisionId = EstadosSalida.PrimeraRevision.Aprobada,
                TieneConsolidado        = true,
                ConsolidadoAt           = cabecera.UploadedAt,
                // En obra el documento lo firman dos (administrador y residente): mientras falte
                // alguno FirmadoAt sigue en null y el paso de la firma sigue siendo de la jefatura.
                // A quién se espera lo dice el propio modal, en su bloque de firmas.
                FirmadoAt               = cabecera.FirmadoAt,
                EstadoReembolsoId       = EstadosSalida.Reembolso.IdFromNombre(cabecera.EstadoReembolso)
                                          ?? EstadosSalida.Reembolso.Pendiente,
                // El origen no da texto: decide si el rojo va en la firma o en Tesorería.
                ObservacionOrigenId     = EstadosSalida.OrigenObservacionReembolso.IdFromNombre(
                                              cabecera.ObservacionReembolsoOrigen),
                Mixto                   = cabecera.ReembolsoMixto,
            });

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

            var seesAll    = scope.SeesAll;
            var areaIds    = scope.VisibleAreaScopeIds ?? new List<int>();
            var deSusObras = scope.TrabajadoresDeSusObras ?? new List<int>();
            var uid        = scope.CurrentUserId;

            // Trabajadores con al menos una salida ya rendida: los demás todavía no tienen planilla
            // y, por lo tanto, tampoco consolidado.
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

            // Las firmas de cada documento y en qué punto de la cadena está ESTE usuario: un
            // consolidado de obra lo firman dos (el administrador y detrás el residente) y hasta
            // que no estén las dos sus salidas siguen Pendientes. Los trabajadores con los que se
            // resuelven los firmantes son los MISMOS que usa la decisión (ver
            // DecidiblesPorUsuarioAsync), así que la pantalla no puede ofrecer algo que la
            // escritura después rechace.
            var workersPorConsolidado = grupos.ToDictionary(
                g => g.Key,
                g => (IReadOnlyCollection<int>)g.Value
                    .SelectMany(x => x.Salidas)
                    .Where(s => porDecidir.Contains(s.Id))
                    .Select(s => s.WorkerId)
                    .Distinct()
                    .ToList());

            var firmasPorConsolidado = await EstadoDeFirmasAsync(ctx, workersPorConsolidado, currentUserId);

            // Quién adjuntó cada consolidado y bajo qué razón social: la del consolidador.
            var subidoPor = await SubidoPorAsync(ctx, grupos.Keys.ToList());
            var razones   = await RazonSocialConsolidador.LoadPorUsuarioAsync(
                ctx, subidoPor.Values.Select(x => x.UserId).Distinct().ToList());

            decimal MontoCompletoDe(int rendicionId) => enTabla.TryGetValue(rendicionId, out var fila)
                ? fila.MontoTotalPlanilla
                : totalesFuera.GetValueOrDefault(rendicionId);

            bool ReembolsoAbierto(int rendicionId) =>
                agrupables.TryGetValue(rendicionId, out var a) && a.ReembolsoAbierto;

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
                                ReembolsoAbierto   = ReembolsoAbierto(kv.Key),
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
                            ReembolsoAbierto   = ReembolsoAbierto(p.Id),
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

                var firmas = firmasPorConsolidado.GetValueOrDefault(consolidadoId) ?? new EstadoFirmas();

                items.Add(new ConsolidadoListItemDto
                {
                    Id              = dto.Id,
                    Codigo          = dto.Codigo,
                    NumeroReembolso = dto.NumeroReembolso,
                    PlanillaGrupalUrl      = dto.PlanillaGrupalUrl,
                    PlanillaGrupalFilename = dto.PlanillaGrupalFilename,
                    PlanillaGrupalFirmadoUrl      = dto.PlanillaGrupalFirmadoUrl,
                    PlanillaGrupalFirmadoFilename = dto.PlanillaGrupalFirmadoFilename,
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

                    // Firmar no es decidir: con una sola de las dos firmas el documento sigue
                    // esperando, y quien ya firmó no vuelve a aprobar (vuelve a firmar, si hace
                    // falta, mientras el que sigue no haya firmado).
                    Firmas             = firmas.Puestas,
                    FirmasPendientes   = firmas.Pendientes,
                    YaFirme            = firmas.YaFirme,
                    EsperaFirmaPrevia  = firmas.EsperaFirmaPrevia,
                    PuedeVolverAFirmar = firmas.PuedeVolverAFirmar,

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
                    // Mismo criterio que la subida: el documento nuevo cubre las planillas que
                    // siguen abiertas; sin ninguna, el consolidado ya quedó firmado y no se toca.
                    PuedeReemplazar = puedeConsolidar && rendiciones.Any(r => r.ReembolsoAbierto),
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
                .Select(r => new { r.Id, r.PdfUrl, r.PdfFilename, r.PdfFirmadoUrl })
                .ToListAsync();

            // El consolidado de cada salida con la MISMA precedencia que usa todo el módulo (el
            // propio de la salida si lo tiene, si no el de su planilla): así una planilla vieja con
            // consolidados por salida también se firma, sin un caso especial acá.
            var consolidadoPorSolicitud = await ConsolidadoS10Loader.LoadAsync(
                ctx, solicitudes.ToDictionary(s => s.Id, s => s.RendicionId));

            var consolidadoIds = consolidadoPorSolicitud.Values.Select(c => c.Id).Distinct().ToList();
            var consolidados = await ctx.GaConsolidadoS10
                .Where(c => consolidadoIds.Contains(c.Id))
                .Select(c => new
                {
                    c.Id, c.PdfUrl, c.PdfFilename, c.PdfFirmadoUrl,
                    c.PlanillaGrupalUrl, c.PlanillaGrupalFilename, c.PlanillaGrupalFirmadoUrl,
                })
                .ToDictionaryAsync(c => c.Id, c => c);

            // Un consolidado compartido puede llegar ya firmado por otro jefe, que aprobó otra de
            // sus planillas: la firma nueva se suma sobre esa copia, en el lugar siguiente, en vez
            // de pisarla. Y si quien aprueba ya lo firmó, no se vuelve a firmar.
            var firmantes = await FirmantesPorConsolidadoAsync(ctx, consolidadoIds);

            // Y el TURNO: las firmas van en orden (primero el administrador de obra, después el
            // residente), así que un consolidado al que todavía le falta una firma anterior a la
            // mía no se me ofrece. Sin esto el residente podría firmar un documento que el
            // administrador no vio, que es justo lo que el orden existe para impedir.
            var fueraDeTurno = await ConsolidadosFueraDeTurnoAsync(
                ctx, consolidadoIds, solicitudes.Select(s => s.WorkerId).Distinct().ToList(),
                reviewerUserId);

            var resultado = new List<PlanillaParaFirmarDto>(planillas.Count);
            var hayFueraDeTurno = false;

            foreach (var p in planillas)
            {
                // Las salidas de la planilla que este usuario puede firmar HOY, con el consolidado
                // que respalda a cada una. Una salida cuyo documento todavía espera una firma
                // ANTERIOR a la suya queda fuera: sin esto el residente podía aprobar (y con eso
                // dar por pagable) un consolidado que el administrador de obra ni vio.
                var porSolicitud = new Dictionary<int, int>();
                foreach (var sid in porRendicion[p.Id])
                {
                    if (!consolidadoPorSolicitud.TryGetValue(sid, out var dto)) continue;
                    if (!consolidados.ContainsKey(dto.Id))                      continue;
                    if (fueraDeTurno.Contains(dto.Id)) { hayFueraDeTurno = true; continue; }
                    porSolicitud[sid] = dto.Id;
                }

                // Sin ninguna, la planilla no se toca: firmarla igual dejaría su PDF con una firma
                // nueva y sus salidas decididas sin que al usuario le tocara.
                if (porSolicitud.Count == 0) continue;

                var docs = porSolicitud.Values
                    .Distinct()
                    // Un consolidado que este usuario YA firmó no se vuelve a estampar —firmar dos
                    // veces no lo completa—, pero sus salidas siguen en juego: pasan a Firmado solo
                    // si el documento ya no debe ninguna firma, y de eso se ocupa la escritura con
                    // ConsolidadoPorSolicitud.
                    .Where(cid => !(firmantes.TryGetValue(cid, out var yaFirmaron) && yaFirmaron.Contains(reviewerUserId)))
                    .Select(cid =>
                    {
                        var c = consolidados[cid];
                        var previas = c.PdfFirmadoUrl != null && firmantes.TryGetValue(cid, out var yaFirmaron)
                            ? yaFirmaron.Count
                            : 0;

                        // La planilla grupal acumula las firmas igual que el consolidado. Si llega
                        // sin copia firmada aunque ya haya firmas —las que se estamparon antes de
                        // que la grupal se firmara—, esta es su primera.
                        var grupalFirmada = previas > 0 && c.PlanillaGrupalFirmadoUrl != null;

                        return new DocumentoParaFirmarDto
                        {
                            Id       = cid,
                            Url      = previas > 0 ? c.PdfFirmadoUrl! : c.PdfUrl,
                            Filename = c.PdfFilename,
                            Slot     = previas,
                            GrupalUrl = c.PlanillaGrupalUrl == null
                                ? null
                                : grupalFirmada ? c.PlanillaGrupalFirmadoUrl : c.PlanillaGrupalUrl,
                            GrupalFilename = c.PlanillaGrupalFilename,
                            GrupalSlot     = grupalFirmada ? previas : 0,
                        };
                    })
                    .ToList();

                // La planilla acumula las firmas igual que su consolidado: desde el 2026-09-21 un
                // consolidado se decide ENTERO, así que quien lo firma firma todas sus planillas y
                // la firma va en el mismo lugar. Partir siempre del original hacía que la segunda
                // firma —la del residente— borrara la del administrador de obra.
                var slotPlanilla    = docs.Count == 0 ? 0 : docs.Max(d => d.Slot);
                var planillaFirmada = slotPlanilla > 0 && p.PdfFirmadoUrl != null;

                resultado.Add(new PlanillaParaFirmarDto
                {
                    RendicionId             = p.Id,
                    SolicitudIds            = porSolicitud.Keys.ToList(),
                    PlanillaUrl             = planillaFirmada ? p.PdfFirmadoUrl! : p.PdfUrl,
                    PlanillaFilename        = p.PdfFilename,
                    PlanillaSlot            = planillaFirmada ? slotPlanilla : 0,
                    Consolidados            = docs,
                    ConsolidadoPorSolicitud = porSolicitud,
                });
            }

            // Nada que firmar PORQUE todavía no le toca: se dice así y no con el 400 genérico de
            // "no hay nada por decidir", que mandaría a buscar el problema donde no está.
            if (resultado.Count == 0 && hayFueraDeTurno)
                throw new AbrilException(
                    "Todavía no te toca firmar este consolidado: falta la firma de quien va antes que tú.", 409);

            return resultado;
        }

        public async Task<ReembolsoFirmaResultDto> AprobarReembolsoFirmado(
            IReadOnlyCollection<PlanillaFirmadaDto> planillas, int reviewerUserId, DateTimeOffset firmadoAt)
        {
            var resultado = new ReembolsoFirmaResultDto();
            if (planillas.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();
            // La hora que quedó impresa en el pie de las firmas: la base y el papel dicen lo mismo.
            var now = firmadoAt;

            var solicitudIds = planillas.SelectMany(p => p.SolicitudIds).Distinct().ToList();
            var rendicionIds = planillas.Select(p => p.RendicionId).Distinct().ToList();

            var rendiciones = await ctx.GaRendicion
                .Where(r => rendicionIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id);

            // Dos conjuntos distintos y no uno: los que hay que ESTAMPAR (los que este usuario no
            // había firmado) y los que CUBREN las salidas. Con una sola firma puesta, volver a
            // aprobar no estampa nada, pero el documento sigue debiendo la otra firma y sus salidas
            // no pueden pasar a Firmado — que es justo lo que se escapaba cuando el conteo salía
            // de lo estampado.
            var consolidadoIds = planillas.SelectMany(p => p.Consolidados.Keys).Distinct().ToList();
            var cubrientes     = planillas.SelectMany(p => p.ConsolidadoPorSolicitud.Values).Distinct().ToList();
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

            // La firma de ESTE usuario sobre cada consolidado tocado queda registrada como fila: es
            // lo que permite que después firme otro al costado sin pisarla, y lo que se contrasta
            // contra las firmas que el área exige antes de dar el documento por firmado.
            var yaFirmadosPorMi = await ctx.GaConsolidadoS10Firma
                .Where(f => f.State && f.FirmadoPorId == reviewerUserId
                         && consolidadoIds.Contains(f.ConsolidadoS10Id))
                .Select(f => f.ConsolidadoS10Id)
                .ToListAsync();

            // Una persona puede tener más de una ficha (reingresos): gana la vigente, el mismo
            // criterio que GetFirmante usa para el puesto del pie. Que la firma quede con la ficha
            // buena no es cosmético — es contra ella que se contrasta quién falta firmar, aunque
            // FirmoYa cubra además el caso de que el aprobador esté configurado con otra.
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var fichaDelFirmante = await (
                from w in ctx.Worker.AsNoTracking()
                join per in ctx.Person.AsNoTracking() on w.PersonId equals per.PersonId
                where per.UserId == reviewerUserId && w.State
                orderby ctx.WorkerVinculacion.Any(v =>
                            v.WorkerId == w.Id && (v.FechaFin == null || v.FechaFin >= hoy)) descending,
                        w.WorkersEstadoId == WorkersEstadoIds.Activo descending,
                        w.Id descending
                select (int?)w.Id
            ).FirstOrDefaultAsync();

            foreach (var p in planillas)
            {
                // Si ninguna de sus salidas sigue viva, la planilla no se toca: sus PDF firmados
                // quedan en SharePoint sin referencia, que es mejor que pisar una firma ajena.
                if (!p.SolicitudIds.Any(vivas.Contains)) continue;

                // Sin copia nueva no se toca nada: la que ya existe lleva la firma de este mismo
                // usuario, puesta cuando aprobó otra planilla del mismo consolidado.
                if (p.Planilla != null && rendiciones.TryGetValue(p.RendicionId, out var r))
                {
                    r.PdfFirmadoUrl      = p.Planilla.Url;
                    r.PdfFirmadoItemId   = p.Planilla.ItemId;
                    r.PdfFirmadoFilename = p.Planilla.Filename;
                    r.FirmadoPorId       = reviewerUserId;
                    r.FirmadoAt          = now;
                }

                foreach (var (consolidadoId, firmado) in p.Consolidados)
                {
                    if (!consolidados.TryGetValue(consolidadoId, out var c)) continue;
                    c.PdfFirmadoUrl      = firmado.S10.Url;
                    c.PdfFirmadoItemId   = firmado.S10.ItemId;
                    c.PdfFirmadoFilename = firmado.S10.Filename;
                    c.FirmadoPorId       = reviewerUserId;
                    c.FirmadoAt          = now;

                    if (firmado.Grupal != null)
                    {
                        c.PlanillaGrupalFirmadoUrl      = firmado.Grupal.Url;
                        c.PlanillaGrupalFirmadoItemId   = firmado.Grupal.ItemId;
                        c.PlanillaGrupalFirmadoFilename = firmado.Grupal.Filename;
                    }

                    // Una fila por firma. El slot es el lugar que ocupó en la hoja, para que la
                    // siguiente se estampe al costado y no encima.
                    if (!yaFirmadosPorMi.Contains(consolidadoId))
                    {
                        ctx.GaConsolidadoS10Firma.Add(new GaConsolidadoS10Firma
                        {
                            ConsolidadoS10Id = consolidadoId,
                            FirmadoPorId     = reviewerUserId,
                            WorkerId         = fichaDelFirmante,
                            Slot             = firmado.Slot,
                            FirmadoAt        = now,
                            State            = true,
                            CreatedAt        = now,
                        });
                        yaFirmadosPorMi.Add(consolidadoId);
                        resultado.ConsolidadosFirmados.Add(consolidadoId);
                    }
                }
            }

            // ── ¿Queda firmado el documento, o falta alguien? ──────────────────
            // Un consolidado de obra lo firman DOS (administrador y residente). La salida solo pasa
            // a "Firmado" —que es lo que Tesorería ve como pagable— cuando están todas las firmas
            // que su área exige; con una sola se queda esperando a la otra.
            var faltanFirmas = await ConsolidadosIncompletosAsync(
                ctx, cubrientes, solicitudes.Select(s => s.WorkerId).Distinct().ToList());

            // El documento de CADA salida, tal como lo resolvió el guard: en una planilla con
            // consolidados por salida (registros antiguos) no todas cuelgan del mismo.
            var consolidadoDeSolicitud = new Dictionary<int, int>();
            foreach (var p in planillas)
                foreach (var (sid, cid) in p.ConsolidadoPorSolicitud)
                    consolidadoDeSolicitud[sid] = cid;

            foreach (var s in solicitudes)
            {
                resultado.Firmadas.Add(s.Id);

                // Aprobar ES la firma. La salida salta a "Firmado" —lo que Tesorería ve como
                // pagable— solo si el consolidado ya reunió TODAS sus firmas; si falta alguna se
                // queda como está, esperando al siguiente firmante, con su firma ya estampada en
                // el papel.
                var incompleto = consolidadoDeSolicitud.TryGetValue(s.Id, out var cid)
                                 && faltanFirmas.Contains(cid);
                if (incompleto)
                {
                    s.UpdatedAt = now;
                    continue;
                }

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

                resultado.Completadas.Add(s.Id);
            }

            // Lo pagable, que es por planilla: solo las que dejaron alguna salida en "Firmado".
            resultado.RendicionesCompletadas = planillas
                .Where(p => p.SolicitudIds.Any(resultado.Completadas.Contains))
                .Select(p => p.RendicionId)
                .Distinct()
                .ToList();

            await ctx.SaveChangesAsync();
            return resultado;
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
        /// De las salidas dadas, las que decide el usuario.
        ///
        /// La unidad es el DOCUMENTO y no la salida (2026-09-21): un consolidado tiene UN firmante
        /// —el mismo que recibe su aviso—, así que las salidas se agrupan por el consolidado que
        /// las cubre y o se decide el documento entero o no se decide nada de él. Antes se resolvía
        /// el revisor de cada trabajador por separado, lo que permitía firmar un documento por
        /// partes.
        ///
        /// Un número fijo de consultas: las fichas del usuario, el consolidado de cada salida y un
        /// solo lote para todos los documentos (ver <c>ResolveFirmantesDeDocumentosAsync</c>).
        /// </summary>
        private async Task<HashSet<int>> DecidiblesPorUsuarioAsync(
            AppDbContext ctx, IReadOnlyCollection<(int Id, int WorkerId)> salidas, int? userId)
        {
            if (!userId.HasValue || salidas.Count == 0) return new();

            var quien = await RevisorDeLaSalida.CargarQuienDecideAsync(ctx, userId);
            if (quien.WorkerIds.Count == 0) return new();

            var solicitudIds = salidas.Select(s => s.Id).Distinct().ToList();

            var consolidadoPorSolicitud = await (
                from s  in ctx.GaSolicitudSalida.AsNoTracking()
                join cr in ctx.GaConsolidadoS10Rendicion.AsNoTracking()
                    on s.RendicionId equals cr.RendicionId
                where solicitudIds.Contains(s.Id) && cr.State
                select new { s.Id, cr.ConsolidadoS10Id }
            ).ToDictionaryAsync(x => x.Id, x => x.ConsolidadoS10Id);

            // Las salidas sin consolidado (todavía) se agrupan por su propio id: cada una es su
            // propio documento hasta que alguna las cubra.
            var grupos = salidas
                .GroupBy(s => consolidadoPorSolicitud.TryGetValue(s.Id, out var c) ? c : -s.Id)
                .ToList();

            var firmantes = await _jefeResolver.ResolveAprobadoresDeDocumentosAsync(
                grupos.ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<int>)g.Select(s => s.WorkerId).Distinct().ToList()),
                PasoAprobacion.Consolidado);

            // El árbol solo hace falta si algún firmante es el fallback de GTH (un área, no una
            // persona): ahí decide cualquiera que cuelgue de ese nodo.
            var necesitaArbol = firmantes.Values.Any(
                fs => fs.Count == 0 || fs.Any(f => f.Persona.WorkerId == null));
            var arbol = necesitaArbol
                ? await RevisorDeLaSalida.CargarArbolAsync(ctx)
                : new Dictionary<int, (int? Padre, string Nombre)>();

            // Un consolidado puede necesitar VARIAS firmas (en obra: administrador y residente), así
            // que basta con estar entre ellas para que el documento le aparezca accionable. Que le
            // toque el turno es otra cosa y la valida la escritura.
            var decidibles = new HashSet<int>();
            foreach (var grupo in grupos)
            {
                var deEste = firmantes.GetValueOrDefault(grupo.Key) ?? new List<AprobadorDocumento>();
                if (!deEste.Any(f => RevisorDeLaSalida.EsElRevisor(quien, f.Persona, arbol)))
                    continue;

                foreach (var salida in grupo) decidibles.Add(salida.Id);
            }

            return decidibles;
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

            var info = await ArmarAvisoJefaturaAsync(ctx, consolidadoId, visibles);
            info.PuedeConsolidar = await PuedeConsolidarAsync(ctx, consolidadoId, userId);
            return info;
        }

        public async Task<AvisoJefaturaInfoDto?> GetAvisoSiguienteFirmante(int consolidadoId)
        {
            // Este aviso no lo pide nadie: lo dispara la firma anterior. Por eso se resuelve sobre
            // el documento ENTERO y no sobre lo que ve un usuario —el que acaba de firmar puede no
            // ver el área del que sigue— y por eso tampoco pregunta quién es el consolidador.
            var todas = await ResolverSolicitudIds(
                new[] { consolidadoId }, new ConsolidadoFiltersDto { SeesAll = true });
            if (todas.Count == 0) return null;

            using var ctx = _factory.CreateDbContext();
            return await ArmarAvisoJefaturaAsync(ctx, consolidadoId, todas);
        }

        /// <summary>
        /// El aviso de un consolidado: qué salidas están esperando una firma y a QUIÉN le toca
        /// ponerla hoy. Lo comparten el aviso que manda el consolidador («Avisar a la jefatura», y
        /// el automático de adjuntar) y el que sale solo cuando alguien firma, para que las dos
        /// vías nombren exactamente al mismo destinatario.
        /// </summary>
        /// <param name="candidatas">Salidas del consolidado ya acotadas (por alcance, o todas).</param>
        private async Task<AvisoJefaturaInfoDto> ArmarAvisoJefaturaAsync(
            AppDbContext ctx, int consolidadoId, List<int> candidatas)
        {
            var info = new AvisoJefaturaInfoDto();

            // Lo que está esperando a la jefatura: rendido, con consolidado y todavía Pendiente. Lo
            // Observado no: ahí la pelota está en el consolidador. Una salida ya firmada por
            // completo tampoco: pasó a Firmado y es de Tesorería.
            var revisables = await IdsConReembolsoRevisableAsync(ctx, candidatas);
            var pendientes = await ctx.GaSolicitudSalida
                .Where(s => revisables.Contains(s.Id) && s.EstadoReembolsoId == EstadosSalida.Reembolso.Pendiente)
                .Select(s => new { s.Id, s.WorkerId })
                .ToListAsync();
            if (pendientes.Count == 0) return info;

            info.SolicitudIds = pendientes.Select(p => p.Id).ToList();

            // Quiénes firman el documento: se resuelve por DOCUMENTO y no por trabajador —eso daría
            // varios dueños para un solo papel— con el mismo resolver que después habilita el botón
            // de firmar en esta pantalla.
            var firmantes = await _jefeResolver.ResolveAprobadoresDeDocumentoAsync(
                pendientes.Select(p => p.WorkerId).Distinct().ToList(),
                PasoAprobacion.Consolidado);

            // Y de ellos, SOLO a quien le toca ahora: las firmas van en cadena (en obra el
            // administrador y recién después el residente), así que al segundo se le avisa cuando
            // el primero firmó y no antes. Ver FirmaEnTurno.
            var enTurno = FirmaEnTurno.De(firmantes, await FirmasPuestasAsync(ctx, consolidadoId));

            var jefaturas = enTurno
                .Select(f => f.Persona)
                .Where(p => !string.IsNullOrWhiteSpace(p.Email))
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

        /// <summary>Fichas que ya firmaron un consolidado (<c>ga_consolidado_s10_firma</c>).</summary>
        private static async Task<HashSet<int>> FirmasPuestasAsync(AppDbContext ctx, int consolidadoId)
            => (await ctx.GaConsolidadoS10Firma.AsNoTracking()
                .Where(f => f.State && f.ConsolidadoS10Id == consolidadoId && f.WorkerId != null)
                .Select(f => f.WorkerId!.Value)
                .ToListAsync()).ToHashSet();

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

        public async Task<ReemplazoConsolidadoPlanDto?> GetReemplazoPlan(
            int consolidadoId, ConsolidadoFiltersDto scope, int userId)
        {
            var visibles = await ResolverSolicitudIds(new[] { consolidadoId }, scope);
            if (visibles.Count == 0) return null;

            using var ctx = _factory.CreateDbContext();

            var cubiertas  = await RendicionesCubiertasAsync(ctx, consolidadoId);
            var agrupables = await ConsolidadoS10Agrupacion.LoadPlanillasAsync(ctx, cubiertas);
            var abiertas   = agrupables.Values.Where(a => a.ReembolsoAbierto).ToList();

            var plan = new ReemplazoConsolidadoPlanDto
            {
                PuedeConsolidar      = await PuedeConsolidarAsync(ctx, consolidadoId, userId),
                RendicionIdsAbiertas = abiertas.Select(a => a.RendicionId).OrderBy(id => id).ToList(),
            };
            if (abiertas.Count == 0) return plan;

            // El reemplazo deja el reembolso Pendiente otra vez: el aviso va a quien va a poder
            // firmarlo, que se resuelve por DOCUMENTO y no por trabajador.
            var firmantesCorreccion = await _jefeResolver.ResolveAprobadoresDeDocumentoAsync(
                abiertas.SelectMany(a => a.WorkerIds).Distinct().ToList(),
                PasoAprobacion.Consolidado);

            // Y solo al PRIMERO de la cadena: reemplazar crea un consolidado NUEVO, sin ninguna
            // firma puesta, así que las que tenga el que se está reemplazando no cuentan.
            plan.JefaturaEmails = FirmaEnTurno.De(firmantesCorreccion, new HashSet<int>())
                .Select(f => f.Persona.Email?.Trim() ?? string.Empty)
                .Where(e => e.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

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

        public async Task<List<ConsolidadoParaRefirmarDto>> GetConsolidadosParaVolverAFirmar(
            IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope, int userId)
        {
            var ids = consolidadoIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return new();

            // El mismo recorte que la decisión: mandar un id no alcanza salidas que el usuario no ve.
            var visibles = await ResolverSolicitudIds(ids, scope);
            if (visibles.Count == 0)
                throw new AbrilException("El Consolidado del S10 no existe o no está en tu alcance.", 404);

            using var ctx = _factory.CreateDbContext();

            // Solo lo que sigue esperando una firma: un documento completo ya pasó a Tesorería y su
            // respaldo no se vuelve a tocar.
            var revisables = await IdsConReembolsoRevisableAsync(ctx, visibles);
            if (revisables.Count == 0)
                throw new AbrilException(await MotivoSinNadaQueDecidirAsync(ctx, visibles), 400);

            var salidas = await ctx.GaSolicitudSalida.AsNoTracking()
                .Where(s => revisables.Contains(s.Id))
                .Select(s => new { s.Id, s.WorkerId, s.RendicionId })
                .ToListAsync();

            var consolidadoPorSolicitud = await ConsolidadoS10Loader.LoadAsync(
                ctx, salidas.ToDictionary(s => s.Id, s => s.RendicionId));

            var workersPorConsolidado = salidas
                .Where(s => consolidadoPorSolicitud.ContainsKey(s.Id))
                .GroupBy(s => consolidadoPorSolicitud[s.Id].Id)
                .Where(g => ids.Contains(g.Key))
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<int>)g.Select(s => s.WorkerId).Distinct().ToList());

            var estados = await EstadoDeFirmasAsync(ctx, workersPorConsolidado, userId);

            var elegibles = estados.Where(kv => kv.Value.PuedeVolverAFirmar).Select(kv => kv.Key).ToList();
            if (elegibles.Count == 0)
                throw new AbrilException(
                    "No puedes volver a firmar este consolidado: o todavía no lo firmaste, o ya lo firmó "
                    + "quien va detrás de ti.", 409);

            // Se parte SIEMPRE del original: la copia firmada se rehace entera, con las mismas
            // firmas que tenía y la de este usuario al día.
            var documentos = await ctx.GaConsolidadoS10.AsNoTracking()
                .Where(c => elegibles.Contains(c.Id))
                .Select(c => new
                {
                    c.Id, c.PdfUrl, c.PdfFilename, c.PlanillaGrupalUrl, c.PlanillaGrupalFilename,
                })
                .ToListAsync();

            var firmas = await ctx.GaConsolidadoS10Firma.AsNoTracking()
                .Where(f => f.State && elegibles.Contains(f.ConsolidadoS10Id))
                .Select(f => new { f.Id, f.ConsolidadoS10Id, f.FirmadoPorId, f.Slot, f.FirmadoAt })
                .ToListAsync();

            // Las planillas que firmó ESTE usuario: su copia firmada se rehace también, para que la
            // fecha impresa sea la misma en todo el juego de documentos.
            var vinculos = await ctx.GaConsolidadoS10Rendicion.AsNoTracking()
                .Where(cr => cr.State && elegibles.Contains(cr.ConsolidadoS10Id))
                .Select(cr => new { cr.ConsolidadoS10Id, cr.RendicionId })
                .ToListAsync();

            var rendicionIds = vinculos.Select(v => v.RendicionId).Distinct().ToList();
            var planillas = await ctx.GaRendicion.AsNoTracking()
                .Where(r => rendicionIds.Contains(r.Id) && r.FirmadoPorId == userId)
                .Select(r => new { r.Id, r.PdfUrl, r.PdfFilename })
                .ToDictionaryAsync(r => r.Id);

            return documentos.Select(c => new ConsolidadoParaRefirmarDto
            {
                Id             = c.Id,
                PdfUrl         = c.PdfUrl,
                PdfFilename    = c.PdfFilename,
                GrupalUrl      = c.PlanillaGrupalUrl,
                GrupalFilename = c.PlanillaGrupalFilename,
                Firmas = firmas
                    .Where(f => f.ConsolidadoS10Id == c.Id)
                    .OrderBy(f => f.Slot).ThenBy(f => f.Id)
                    .Select(f => new FirmaPuestaDto
                    {
                        Id           = f.Id,
                        FirmadoPorId = f.FirmadoPorId,
                        Slot         = f.Slot,
                        FirmadoAt    = f.FirmadoAt,
                    })
                    .ToList(),
                Planillas = vinculos
                    .Where(v => v.ConsolidadoS10Id == c.Id && planillas.ContainsKey(v.RendicionId))
                    .Select(v => new PlanillaParaRefirmarDto
                    {
                        RendicionId = v.RendicionId,
                        PdfUrl      = planillas[v.RendicionId].PdfUrl,
                        PdfFilename = planillas[v.RendicionId].PdfFilename,
                    })
                    .ToList(),
            }).ToList();
        }

        public async Task<int> RegistrarVolverAFirmar(
            IReadOnlyCollection<ConsolidadoRefirmadoDto> refirmados, int userId, DateTimeOffset firmadoAt)
        {
            if (refirmados.Count == 0) return 0;

            using var ctx = _factory.CreateDbContext();

            var ids = refirmados.Select(r => r.ConsolidadoId).Distinct().ToList();

            var consolidados = await ctx.GaConsolidadoS10
                .Where(c => ids.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            var rendicionIds = refirmados.SelectMany(r => r.Planillas.Keys).Distinct().ToList();
            var rendiciones = await ctx.GaRendicion
                .Where(r => rendicionIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id);

            // NO se agrega una fila: es la MISMA firma, con la fecha al día. Agregar otra la
            // contaría dos veces contra las que el área exige y daría el documento por completo.
            var mias = await ctx.GaConsolidadoS10Firma
                .Where(f => f.State && f.FirmadoPorId == userId && ids.Contains(f.ConsolidadoS10Id))
                .ToListAsync();

            foreach (var r in refirmados)
            {
                if (consolidados.TryGetValue(r.ConsolidadoId, out var c))
                {
                    c.PdfFirmadoUrl      = r.S10.Url;
                    c.PdfFirmadoItemId   = r.S10.ItemId;
                    c.PdfFirmadoFilename = r.S10.Filename;
                    c.FirmadoPorId       = userId;
                    c.FirmadoAt          = firmadoAt;

                    if (r.Grupal != null)
                    {
                        c.PlanillaGrupalFirmadoUrl      = r.Grupal.Url;
                        c.PlanillaGrupalFirmadoItemId   = r.Grupal.ItemId;
                        c.PlanillaGrupalFirmadoFilename = r.Grupal.Filename;
                    }
                }

                foreach (var (rendicionId, archivo) in r.Planillas)
                {
                    if (!rendiciones.TryGetValue(rendicionId, out var rend)) continue;
                    rend.PdfFirmadoUrl      = archivo.Url;
                    rend.PdfFirmadoItemId   = archivo.ItemId;
                    rend.PdfFirmadoFilename = archivo.Filename;
                    rend.FirmadoPorId       = userId;
                    rend.FirmadoAt          = firmadoAt;
                }

                foreach (var f in mias.Where(f => f.ConsolidadoS10Id == r.ConsolidadoId))
                    f.FirmadoAt = firmadoAt;
            }

            await ctx.SaveChangesAsync();
            return refirmados.Count;
        }

        public async Task<ProximaFirmaDto> GetProximaFirma(
            IEnumerable<int> consolidadoIds, ConsolidadoFiltersDto scope, int? userId)
        {
            var resultado = new ProximaFirmaDto();

            var ids = consolidadoIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return resultado;

            // El mismo recorte que la decisión: lo que el usuario no ve no entra en la cuenta.
            var visibles = await ResolverSolicitudIds(ids, scope);
            if (visibles.Count == 0) return resultado;

            using var ctx = _factory.CreateDbContext();

            // Solo lo que de verdad está esperando una firma: lo ya firmado por completo es de
            // Tesorería y no cambia de manos con esta decisión.
            var revisables = await IdsConReembolsoRevisableAsync(ctx, visibles);
            if (revisables.Count == 0) return resultado;

            var salidas = await ctx.GaSolicitudSalida.AsNoTracking()
                .Where(s => revisables.Contains(s.Id))
                .Select(s => new { s.Id, s.WorkerId, s.RendicionId })
                .ToListAsync();

            var consolidadoPorSolicitud = await ConsolidadoS10Loader.LoadAsync(
                ctx, salidas.ToDictionary(s => s.Id, s => s.RendicionId));

            var workersPorConsolidado = salidas
                .Where(s => consolidadoPorSolicitud.ContainsKey(s.Id))
                .GroupBy(s => consolidadoPorSolicitud[s.Id].Id)
                .Where(g => ids.Contains(g.Key))
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyCollection<int>)g.Select(s => s.WorkerId).Distinct().ToList());

            foreach (var estado in (await EstadoDeFirmasAsync(ctx, workersPorConsolidado, userId)).Values)
            {
                if (estado.SeCompletaConMiFirma) resultado.AlgunoSeCompleta = true;
                resultado.Emails.AddRange(estado.ProximosEmails);
            }

            resultado.Emails = resultado.Emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return resultado;
        }

        public async Task<FirmanteDto> GetFirmante(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var nombre = await ctx.Person
                .Where(p => p.UserId == userId && p.FullName != null)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync();

            // Una persona puede tener más de una ficha (reingresos anteriores a la fusión): gana la
            // vigente, igual que en el resto de los lookups usuario → ficha. Sin ordenar saldría
            // el puesto de una ficha retirada.
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var puesto = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .OrderByDescending(w => ctx.WorkerVinculacion.Any(v =>
                    v.WorkerId == w.Id && (v.FechaFin == null || v.FechaFin >= hoy)))
                .ThenByDescending(w => w.WorkersEstadoId == WorkersEstadoIds.Activo ? 1 : 0)
                .ThenByDescending(w => w.Id)
                .Select(w => w.PuestoCatalogo != null ? w.PuestoCatalogo.Nombre : null)
                .FirstOrDefaultAsync();

            return new FirmanteDto
            {
                // El nombre viene en MAYÚSCULAS; en el pie va como nombre propio ("Roberto Vidal").
                // El puesto no: es un nombre de catálogo y puede traer siglas (SSOMA, TI).
                Nombre = string.IsNullOrWhiteSpace(nombre)
                    ? string.Empty
                    : System.Globalization.CultureInfo.GetCultureInfo("es-PE").TextInfo
                        .ToTitleCase(nombre.Trim().ToLowerInvariant()),
                Puesto = string.IsNullOrWhiteSpace(puesto) ? null : puesto.Trim(),
            };
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
                query, ctx, filters.CurrentUserId, filters.SeesAll, filters.VisibleAreaScopeIds,
                filters.TrabajadoresDeSusObras);
        }

        /// <summary>Copia solo el alcance del usuario, sin los filtros de la pantalla.</summary>
        private static ConsolidadoFiltersDto SoloVisibilidad(ConsolidadoFiltersDto scope) => new()
        {
            CurrentUserId          = scope.CurrentUserId,
            SeesAll                = scope.SeesAll,
            VisibleAreaScopeIds    = scope.VisibleAreaScopeIds,
            TrabajadoresDeSusObras = scope.TrabajadoresDeSusObras,
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
        /// <summary>
        /// Quiénes ya firmaron cada consolidado, por <c>app_user</c>.
        ///
        /// Sale de <c>ga_consolidado_s10_firma</c> y ya NO de <c>ga_rendicion.firmado_por_id</c>:
        /// esa columna admite UNA firma por planilla, y desde el 2026-09-21 en obra firman el
        /// administrador de obra y el residente sobre las MISMAS planillas. La columna se sigue
        /// escribiendo —es la que referencia el PDF firmado de cada planilla— pero ya no es la
        /// fuente de "quiénes firmaron".
        /// </summary>
        private static async Task<Dictionary<int, HashSet<int>>> FirmantesPorConsolidadoAsync(
            AppDbContext ctx, List<int> consolidadoIds)
        {
            if (consolidadoIds.Count == 0) return new();

            var filas = await ctx.GaConsolidadoS10Firma.AsNoTracking()
                .Where(f => f.State && consolidadoIds.Contains(f.ConsolidadoS10Id))
                .Select(f => new { f.ConsolidadoS10Id, f.FirmadoPorId })
                .ToListAsync();

            return filas
                .GroupBy(f => f.ConsolidadoS10Id)
                .ToDictionary(g => g.Key, g => g.Select(f => f.FirmadoPorId).ToHashSet());
        }

        /// <summary>
        /// De los consolidados dados, aquellos en los que a ESTE usuario todavía no le toca firmar:
        /// alguien que va antes que él en el orden de firmas no firmó aún.
        ///
        /// El orden sale del mismo sitio que los aprobadores (<c>area_revisores_rendicion</c> por
        /// <c>orden_prioridad</c>, o el algoritmo: administrador de obra y después residente). Quien
        /// no figura entre los aprobadores no queda fuera de turno por esta vía —de eso se ocupa el
        /// guard de siempre—, y un documento con un solo firmante nunca cae acá.
        /// </summary>
        private async Task<HashSet<int>> ConsolidadosFueraDeTurnoAsync(
            AppDbContext ctx, List<int> consolidadoIds, List<int> workerIds, int reviewerUserId)
        {
            if (consolidadoIds.Count == 0 || workerIds.Count == 0) return new();

            var estados = await EstadoDeFirmasAsync(
                ctx,
                consolidadoIds.ToDictionary(id => id, _ => (IReadOnlyCollection<int>)workerIds),
                reviewerUserId);

            return estados.Where(kv => kv.Value.EsperaFirmaPrevia).Select(kv => kv.Key).ToHashSet();
        }

        /// <summary>
        /// En qué punto de la cadena de firmas está cada consolidado, visto por UN usuario: quiénes
        /// firmaron, quiénes faltan, y qué puede hacer él hoy.
        ///
        /// Es el ÚNICO lugar donde se cruzan las firmas puestas
        /// (<c>ga_consolidado_s10_firma</c>) con las que el área exige (el algoritmo o
        /// <c>area_revisores_rendicion</c>), y por eso lo miran las dos cosas que tienen que decir
        /// lo mismo: los botones de la pantalla y el guard que deja firmar. Sin esto, la pantalla
        /// seguía ofreciendo "Aprobar" a quien ya había firmado.
        /// </summary>
        /// <param name="workersPorConsolidado">
        /// Los trabajadores con los que se resuelven los firmantes de cada documento. Es el mismo
        /// recorte que hace la decisión: un documento se resuelve entero, no trabajador por
        /// trabajador.
        /// </param>
        private async Task<Dictionary<int, EstadoFirmas>> EstadoDeFirmasAsync(
            AppDbContext ctx,
            IReadOnlyDictionary<int, IReadOnlyCollection<int>> workersPorConsolidado,
            int? reviewerUserId)
        {
            var estados = workersPorConsolidado.Keys.ToDictionary(id => id, _ => new EstadoFirmas());
            if (estados.Count == 0) return estados;

            var consolidadoIds = estados.Keys.ToList();

            // Mis fichas: los aprobadores se resuelven por workers.id y no por usuario, y una
            // persona puede tener más de una (reingresos).
            var misFichas = reviewerUserId == null
                ? new List<int>()
                : await (
                    from w in ctx.Worker.AsNoTracking()
                    join per in ctx.Person.AsNoTracking() on w.PersonId equals per.PersonId
                    where per.UserId == reviewerUserId && w.State
                    select w.Id
                ).ToListAsync();

            var conTrabajadores = workersPorConsolidado
                .Where(kv => kv.Value.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var esperados = conTrabajadores.Count == 0
                ? new Dictionary<int, List<AprobadorDocumento>>()
                : await _jefeResolver.ResolveAprobadoresDeDocumentosAsync(
                    conTrabajadores, PasoAprobacion.Consolidado);

            // Las firmas puestas, con quién las puso y con qué cargo: es lo que la pantalla muestra
            // al lado del estado para explicar por qué el reembolso sigue Pendiente.
            var puestas = await ctx.GaConsolidadoS10Firma.AsNoTracking()
                .Where(f => f.State && consolidadoIds.Contains(f.ConsolidadoS10Id))
                .Select(f => new
                {
                    f.ConsolidadoS10Id,
                    f.FirmadoPorId,
                    f.WorkerId,
                    f.Slot,
                    f.FirmadoAt,
                    Nombre = ctx.Person.Where(p => p.UserId == f.FirmadoPorId)
                                 .Select(p => p.FullName).FirstOrDefault(),
                    Puesto = ctx.Worker.Where(w => w.Id == f.WorkerId)
                                 .Select(w => w.PuestoCatalogo != null ? w.PuestoCatalogo.Nombre : null)
                                 .FirstOrDefault(),
                    // La PERSONA de la ficha que firmó. Ver FirmoYa: un reingreso deja varias
                    // fichas y la firma se guarda con una sola.
                    PersonId = ctx.Worker.Where(w => w.Id == f.WorkerId)
                                 .Select(w => w.PersonId).FirstOrDefault(),
                })
                .ToListAsync();

            foreach (var (cid, estado) in estados)
            {
                var filas = puestas
                    .Where(f => f.ConsolidadoS10Id == cid)
                    .OrderBy(f => f.Slot).ThenBy(f => f.FirmadoAt)
                    .ToList();

                var firmaron = filas
                    .Where(f => f.WorkerId != null)
                    .Select(f => f.WorkerId!.Value)
                    .ToHashSet();

                var firmaronPersonas = filas
                    .Where(f => f.PersonId != null)
                    .Select(f => f.PersonId!.Value)
                    .ToHashSet();

                estado.Puestas.AddRange(filas.Select(f => new ConsolidadoFirmaDto
                {
                    Nombre    = NombrePropio(f.Nombre),
                    Puesto    = string.IsNullOrWhiteSpace(f.Puesto) ? null : f.Puesto.Trim(),
                    FirmadoAt = f.FirmadoAt,
                    Yo        = reviewerUserId != null && f.FirmadoPorId == reviewerUserId,
                }));

                estado.YaFirme = reviewerUserId != null && filas.Any(f => f.FirmadoPorId == reviewerUserId);

                var aprobadores = esperados.GetValueOrDefault(cid) ?? new List<AprobadorDocumento>();

                // Un aprobador sin ficha resuelta (el fallback de GTH, que es un área) no se puede
                // contrastar contra las firmas puestas: esos documentos se dan por completos con una
                // firma, igual que antes de que existieran las firmas múltiples. Mismo criterio que
                // ConsolidadosIncompletosAsync, que es quien escribe el estado.
                var conFicha = aprobadores.Where(a => a.Persona.WorkerId != null).ToList();

                bool YaLoFirmo(AprobadorDocumento a) =>
                    FirmoYa(a, firmaron, firmaronPersonas);

                estado.Incompleto = conFicha.Any(a => !YaLoFirmo(a));

                // Sin nombre cargado se nombra por el correo, igual que el aviso a la jefatura: la
                // pantalla tiene que poder decir a quién se está esperando.
                if (estado.Incompleto)
                    estado.Pendientes.AddRange(conFicha
                        .Where(a => !YaLoFirmo(a))
                        .OrderBy(a => a.Orden)
                        .Select(a => string.IsNullOrWhiteSpace(a.Persona.Nombre)
                            ? a.Persona.Email.Trim()
                            : NombrePropio(a.Persona.Nombre))
                        .Where(n => n.Length > 0)
                        .Distinct());

                // Qué quedaría si ESTE usuario firmara ahora: o el documento reúne todas sus
                // firmas, o le pasa el turno al que sigue. Lo mira la confirmación de aprobar, que
                // con una firma pendiente detrás tiene que nombrar al que sigue y no al
                // consolidador. Un documento sin aprobadores con ficha (el fallback de GTH, que es
                // un área) se completa con una firma, mismo criterio que el resto del método.
                var restantes = conFicha
                    .Where(a => !YaLoFirmo(a) && !misFichas.Contains(a.Persona.WorkerId!.Value))
                    .ToList();

                estado.SeCompletaConMiFirma = restantes.Count == 0;
                estado.ProximosEmails.AddRange(FirmaEnTurno.De(restantes, new HashSet<int>())
                    .Select(a => (a.Persona.Email ?? string.Empty).Trim())
                    .Where(c => c.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase));

                if (misFichas.Count == 0) continue;

                var miTurno = conFicha
                    .Where(a => misFichas.Contains(a.Persona.WorkerId!.Value))
                    .Select(a => (int?)a.Orden)
                    .Min();

                if (miTurno == null) continue;

                estado.EsperaFirmaPrevia = conFicha.Any(a => a.Orden < miTurno && !YaLoFirmo(a));

                // Volver a firmar rehace el documento desde el original, así que solo se ofrece
                // mientras nadie DETRÁS haya firmado: si no, habría que volver a estampar una firma
                // ajena con una fecha que ya no le corresponde.
                var firmoAlguienDespues = conFicha.Any(a => a.Orden > miTurno && YaLoFirmo(a));

                estado.PuedeVolverAFirmar = estado.YaFirme && estado.Incompleto && !firmoAlguienDespues;
            }

            return estados;
        }

        /// <summary>
        /// Las firmas de un consolidado vistas por un usuario. Ver <see cref="EstadoDeFirmasAsync"/>.
        /// </summary>
        private sealed class EstadoFirmas
        {
            /// <summary>Las que ya están estampadas, por el lugar que ocupan en la hoja.</summary>
            public List<ConsolidadoFirmaDto> Puestas { get; } = new();

            /// <summary>Nombres de los que faltan, en el orden en que les toca.</summary>
            public List<string> Pendientes { get; } = new();

            /// <summary>Correos de a quién le tocaría firmar si este usuario firmara ahora.</summary>
            public List<string> ProximosEmails { get; } = new();

            /// <summary>Con la firma de este usuario el documento las reuniría todas.</summary>
            public bool SeCompletaConMiFirma { get; set; }

            /// <summary>Este usuario ya firmó.</summary>
            public bool YaFirme { get; set; }

            /// <summary>Al documento le falta alguna de las firmas que su área exige.</summary>
            public bool Incompleto { get; set; }

            /// <summary>Le toca firmar, pero alguien que va antes que él todavía no lo hizo.</summary>
            public bool EsperaFirmaPrevia { get; set; }

            /// <summary>Puede rehacer su firma: ya firmó, falta la del que sigue y nadie detrás firmó.</summary>
            public bool PuedeVolverAFirmar { get; set; }
        }

        /// <summary>
        /// Si este aprobador ya firmó. Se compara por PERSONA y no solo por ficha: un reingreso deja
        /// varias filas en <c>workers</c> para la misma persona, la firma se guarda con una de ellas
        /// y el aprobador puede estar configurado con otra (<c>project.residente_workers_id</c>,
        /// <c>workers_coord_admin_id</c>). Comparando solo fichas, ese documento quedaría esperando
        /// para siempre una firma que ya está puesta y el reembolso no llegaría nunca a Tesorería.
        /// </summary>
        private static bool FirmoYa(
            AprobadorDocumento aprobador, IReadOnlySet<int> fichas, IReadOnlySet<int> personas) =>
            (aprobador.Persona.WorkerId != null && fichas.Contains(aprobador.Persona.WorkerId.Value))
            || (aprobador.Persona.PersonId != null && personas.Contains(aprobador.Persona.PersonId.Value));

        /// <summary>
        /// Un nombre de la base (viene en MAYÚSCULAS) como se muestra y como se imprime en el pie de
        /// la firma: "Roberto Vidal". Vacío si no hay nombre.
        /// </summary>
        private static string NombrePropio(string? nombre) =>
            string.IsNullOrWhiteSpace(nombre)
                ? string.Empty
                : System.Globalization.CultureInfo.GetCultureInfo("es-PE").TextInfo
                    .ToTitleCase(nombre.Trim().ToLowerInvariant());
        /// <summary>
        /// De los consolidados dados, los que TODAVÍA no reunieron todas las firmas que su área
        /// exige. Compara las firmas ya estampadas (incluida la que se está por guardar, que ya
        /// está en el ChangeTracker) contra los aprobadores que resuelve el algoritmo o
        /// <c>area_revisores_rendicion</c>.
        ///
        /// Un aprobador sin ficha resuelta (el fallback de GTH, que es un área) no se puede
        /// contrastar: esos documentos se dan por completos con una firma, que es como funcionaban
        /// antes de que existieran las firmas múltiples.
        /// </summary>
        private async Task<HashSet<int>> ConsolidadosIncompletosAsync(
            AppDbContext ctx, List<int> consolidadoIds, List<int> workerIds)
        {
            var incompletos = new HashSet<int>();
            if (consolidadoIds.Count == 0 || workerIds.Count == 0) return incompletos;

            var esperados = await _jefeResolver.ResolveAprobadoresDeDocumentosAsync(
                consolidadoIds.ToDictionary(id => id, _ => (IReadOnlyCollection<int>)workerIds),
                PasoAprobacion.Consolidado);

            // Lo ya guardado más lo que este SaveChanges va a agregar.
            var puestas = await ctx.GaConsolidadoS10Firma.AsNoTracking()
                .Where(f => f.State && consolidadoIds.Contains(f.ConsolidadoS10Id))
                .Select(f => new { f.ConsolidadoS10Id, f.WorkerId })
                .ToListAsync();

            var nuevas = ctx.ChangeTracker.Entries<GaConsolidadoS10Firma>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => new { e.Entity.ConsolidadoS10Id, e.Entity.WorkerId })
                .ToList();

            // La persona de cada ficha que firmó: la comparación va por persona (ver FirmoYa).
            var fichasFirmantes = puestas.Concat(nuevas)
                .Where(f => f.WorkerId != null)
                .Select(f => f.WorkerId!.Value)
                .Distinct()
                .ToList();

            var personaDeFicha = fichasFirmantes.Count == 0
                ? new Dictionary<int, int?>()
                : await ctx.Worker.AsNoTracking()
                    .Where(w => fichasFirmantes.Contains(w.Id))
                    .Select(w => new { w.Id, w.PersonId })
                    .ToDictionaryAsync(w => w.Id, w => w.PersonId);

            var porConsolidado = consolidadoIds.ToDictionary(id => id, _ => new HashSet<int>());
            var personasPorConsolidado = consolidadoIds.ToDictionary(id => id, _ => new HashSet<int>());

            foreach (var f in puestas.Concat(nuevas))
            {
                if (f.WorkerId == null) continue;
                if (porConsolidado.TryGetValue(f.ConsolidadoS10Id, out var set)) set.Add(f.WorkerId.Value);
                if (personaDeFicha.GetValueOrDefault(f.WorkerId.Value) is int personId
                    && personasPorConsolidado.TryGetValue(f.ConsolidadoS10Id, out var personas))
                    personas.Add(personId);
            }

            foreach (var (cid, aprobadores) in esperados)
            {
                var conFicha = aprobadores.Where(a => a.Persona.WorkerId != null).ToList();
                if (conFicha.Count == 0) continue;

                var firmaron = porConsolidado.TryGetValue(cid, out var set) ? set : new HashSet<int>();
                var personasQueFirmaron = personasPorConsolidado.TryGetValue(cid, out var ps)
                    ? ps
                    : new HashSet<int>();

                if (conFicha.Any(a => !FirmoYa(a, firmaron, personasQueFirmaron))) incompletos.Add(cid);
            }

            return incompletos;
        }

        private static void CopiarCabecera(ConsolidadoListItemDto o, ConsolidadoDetalleDto d)
        {
            d.Id = o.Id; d.Codigo = o.Codigo;
            d.NumeroReembolso = o.NumeroReembolso; d.MontoTotal = o.MontoTotal;
            d.MontoVisible = o.MontoVisible;
            d.PdfUrl = o.PdfUrl; d.PdfFilename = o.PdfFilename;
            d.PlanillaGrupalUrl = o.PlanillaGrupalUrl; d.PlanillaGrupalFilename = o.PlanillaGrupalFilename;
            d.PlanillaGrupalFirmadoUrl = o.PlanillaGrupalFirmadoUrl;
            d.PlanillaGrupalFirmadoFilename = o.PlanillaGrupalFirmadoFilename;
            d.PdfFirmadoUrl = o.PdfFirmadoUrl; d.PdfFirmadoFilename = o.PdfFirmadoFilename;
            d.FirmadoAt = o.FirmadoAt; d.UploadedAt = o.UploadedAt; d.SubidoPor = o.SubidoPor;
            d.Rendiciones = o.Rendiciones; d.Trabajadores = o.Trabajadores; d.SalidasCount = o.SalidasCount;
            d.RazonSocialId = o.RazonSocialId; d.RazonSocial = o.RazonSocial;
            d.Periodo = o.Periodo; d.PeriodoAnio = o.PeriodoAnio; d.PeriodoMes = o.PeriodoMes;
            d.EstadoReembolso = o.EstadoReembolso; d.ReembolsoMixto = o.ReembolsoMixto;
            d.ObservacionReembolso = o.ObservacionReembolso;
            d.ObservacionReembolsoOrigen = o.ObservacionReembolsoOrigen;
            d.PorDecidirCount = o.PorDecidirCount;
            d.Firmas = o.Firmas; d.FirmasPendientes = o.FirmasPendientes;
            d.YaFirme = o.YaFirme; d.EsperaFirmaPrevia = o.EsperaFirmaPrevia;
            d.PuedeVolverAFirmar = o.PuedeVolverAFirmar;
            d.PuedeConsolidar = o.PuedeConsolidar; d.PuedeAvisarJefatura = o.PuedeAvisarJefatura;
            d.JefaturaAvisadaAt = o.JefaturaAvisadaAt; d.PuedeSolicitarCorreccion = o.PuedeSolicitarCorreccion;
            d.PuedeReemplazar = o.PuedeReemplazar;
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
