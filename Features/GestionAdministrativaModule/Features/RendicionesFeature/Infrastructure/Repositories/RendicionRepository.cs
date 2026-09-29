using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Shared.Services;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Rendiciones.Infrastructure.Repositories
{
    /// <summary>
    /// Lee las planillas de rendición del propio trabajador. Todo lo que devuelve está acotado a
    /// sus salidas: una planilla generada por el revisor puede agrupar a varias personas, y esta
    /// pantalla es "Mis Rendiciones".
    /// </summary>
    public class RendicionRepository : IRendicionRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public RendicionRepository(IDbContextFactory<AppDbContext> factory) => _factory = factory;

        public async Task<List<RendicionListItemDto>> GetByUserId(int userId, RendicionFiltersDto? filters = null)
        {
            using var ctx = _factory.CreateDbContext();

            var workerId = await ResolveWorkerIdAsync(ctx, userId);
            if (workerId == null) return new();

            var salidas = await CargarSalidasPropiasAsync(ctx, workerId.Value);
            if (salidas.Count == 0) return new();

            var rendicionIds = salidas.Select(s => s.RendicionId).Distinct().ToList();

            var planillas = await ctx.GaRendicion
                .Where(r => rendicionIds.Contains(r.Id))
                .ToListAsync();

            var consolidados = await ConsolidadoS10Loader.LoadPorRendicionAsync(ctx, rendicionIds);
            var montos       = await MontosPorSolicitudAsync(ctx, salidas.Select(s => s.Id).ToList(), workerId.Value);

            // Total de la planilla ENTERA (todas sus salidas, de todos sus trabajadores): es el
            // importe que se registró en el S10. MontoTotal, en cambio, suma solo las salidas propias.
            var totalesPlanilla = await TotalPlanillaLoader.LoadAsync(ctx, rendicionIds);

            var porRendicion = salidas.GroupBy(s => s.RendicionId).ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<RendicionListItemDto>(planillas.Count);
            foreach (var planilla in planillas)
            {
                if (!porRendicion.TryGetValue(planilla.Id, out var propias) || propias.Count == 0) continue;

                consolidados.TryGetValue(planilla.Id, out var consolidado);
                result.Add(Armar(
                    planilla, propias, consolidado, montos,
                    totalesPlanilla.TryGetValue(planilla.Id, out var totalP) ? totalP : 0m));
            }

            // Más reciente primero: lo que se acaba de rendir es lo que tiene pasos pendientes.
            result = result.OrderByDescending(r => r.RendidoAt).ToList();
            return Filtrar(result, filters);
        }

        public async Task<RendicionDetalleDto?> GetDetalleForUser(int rendicionId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var workerId = await ResolveWorkerIdAsync(ctx, userId);
            if (workerId == null) return null;

            var planilla = await ctx.GaRendicion.FirstOrDefaultAsync(r => r.Id == rendicionId);
            if (planilla == null) return null;

            var propias = (await CargarSalidasPropiasAsync(ctx, workerId.Value, rendicionId));
            if (propias.Count == 0) return null; // no es suya: no existe para esta pantalla

            var consolidados = await ConsolidadoS10Loader.LoadPorRendicionAsync(ctx, new[] { rendicionId });
            consolidados.TryGetValue(rendicionId, out var consolidado);

            var solicitudIds = propias.Select(s => s.Id).ToList();
            var montos       = await MontosPorSolicitudAsync(ctx, solicitudIds, workerId.Value);
            var trayectos    = await CargarTrayectosAsync(ctx, solicitudIds);
            var totalPlanilla = await TotalPlanillaLoader.LoadOneAsync(ctx, rendicionId);

            var cabecera = Armar(planilla, propias, consolidado, montos, totalPlanilla);
            var detalle  = new RendicionDetalleDto();
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

            detalle.Salidas = propias
                .OrderBy(s => s.FechaSalida).ThenBy(s => s.Id)
                .Select(s =>
                {
                    trayectos.TryGetValue(s.Id, out var trList);
                    trList ??= new();
                    var first = trList.FirstOrDefault();
                    var last  = trList.LastOrDefault();
                    return new RendicionSalidaDto
                    {
                        Id                   = s.Id,
                        Codigo               = s.Codigo,
                        FechaSalida          = s.FechaSalida,
                        Motivo               = first?.Motivo ?? string.Empty,
                        LugarOrigen          = first?.LugarOrigen,
                        LugarDestino         = last?.LugarDestino,
                        TrayectosCount       = trList.Count,
                        Monto                = montos.TryGetValue(s.Id, out var m) ? m : 0m,
                        EstadoReembolso      = EstadosSalida.Reembolso.Nombre(s.EstadoReembolsoId),
                        ObservacionReembolso = s.ObservacionReembolso,
                    };
                })
                .ToList();

            return detalle;
        }

        public async Task<(int? WorkerId, string? Email, List<PeriodoOptionDto> Periodos)> GetPeriodos(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // La ficha y el correo de usuario en una sola consulta: los dos los piden los correos
            // de "Enviar a revisión". Mismo criterio de ficha que ResolveWorkerIdAsync.
            var yo = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .Select(w => new
                {
                    w.Id,
                    Email = ctx.User.Where(u => u.UserId == userId).Select(u => u.Email).FirstOrDefault(),
                })
                .FirstOrDefaultAsync();
            if (yo == null) return (null, null, new());

            var workerId = yo.Id;
            var salidas  = await CargarSalidasPropiasAsync(ctx, workerId);
            if (salidas.Count == 0) return (workerId, yo.Email, new());

            // El periodo de una planilla es el mes de su salida más antigua — el mismo criterio
            // que usa la tabla, si no el filtro dejaría fuera planillas que sí muestra.
            var periodos = salidas
                .GroupBy(s => s.RendicionId)
                .Select(g => g.Min(s => s.FechaSalida))
                .Select(f => (f.Year, f.Month))
                .Distinct()
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Month)
                .Select(p => new PeriodoOptionDto
                {
                    Anio  = p.Year,
                    Mes   = p.Month,
                    Label = PlanillaRendicionHelper.EtiquetaMes(p.Year, p.Month),
                })
                .ToList();

            return (workerId, yo.Email, periodos);
        }

        public async Task MarcarEnviadaAPrimeraRevision(int rendicionId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var planilla = await ctx.GaRendicion.FirstOrDefaultAsync(r => r.Id == rendicionId)
                ?? throw new AbrilException("La planilla de rendición no existe.", 404);

            planilla.EstadoPrimeraRevisionId = EstadosSalida.PrimeraRevision.EnRevision;
            planilla.EnviadaRevisionAt       = DateTimeOffset.UtcNow;
            planilla.EnviadaRevisionPorId    = userId;

            await ctx.SaveChangesAsync();
        }

        public async Task<RendicionSolicitanteDto?> GetSolicitante(int rendicionId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            // La planilla ENTERA en una sola consulta: de ella salen el solicitante (la fila del
            // usuario, que es además el guard de propiedad) y el conjunto de trabajadores que
            // identifica al documento. Pedir las dos cosas por separado sería un roundtrip más
            // sobre las mismas filas.
            var filas = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                join u in ctx.User on (int?)per.UserId equals (int?)u.UserId into uGroup
                from u in uGroup.DefaultIfEmpty()
                where s.RendicionId == rendicionId
                select new
                {
                    WorkerId    = w.Id,
                    UserId      = per.UserId,
                    Trabajador  = per.FullName ?? "Trabajador",
                    Email       = u != null ? u.Email : null,
                    // El área del trabajador sale del puesto, no de workers.
                    AreaScopeId = w.PuestoCatalogo != null ? w.PuestoCatalogo.AreaDestinoScopeId : null,
                }
            ).ToListAsync();

            var quien = filas.FirstOrDefault(f => f.UserId == userId);
            if (quien == null) return null;

            return new RendicionSolicitanteDto
            {
                WorkerId            = quien.WorkerId,
                Trabajador          = quien.Trabajador,
                Email               = quien.Email,
                Area                = await ResolveAreaNombreAsync(ctx, quien.AreaScopeId),
                WorkersDeLaPlanilla = filas.Select(f => f.WorkerId).Distinct().ToList(),
            };
        }

        public async Task<int> ContarTrayectos(int rendicionId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var trayectos = await (
                from t in ctx.GaSolicitudTrayecto
                join s in ctx.GaSolicitudSalida on t.SolicitudId equals s.Id
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId
                where s.RendicionId == rendicionId && per.UserId == userId
                select new { t.Id, w.Subarea, t.LugarOrigenId, t.LugarDestinoId }
            ).ToListAsync();

            // Solo los trayectos que se rindieron: el número viaja en el correo a la jefatura y
            // tiene que cuadrar con las filas que trae la planilla adjunta. Por eso se cuenta con
            // la misma regla que las imprime —y que deja fuera al trayecto de S/ 0.00— en vez de
            // con la del motivo, que no mira el importe.
            var importes = await ImporteRendidoLoader.LoadAsync(
                ctx,
                trayectos.Select(t => new ImporteRendidoLoader.TrayectoParaImporte(
                    t.Id, t.Subarea, t.LugarOrigenId, t.LugarDestinoId)).ToList());

            return importes.Count(x => x.Value.EsReembolsable && x.Value.Importe > 0m);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>Nombre del área a la que apunta el nodo (el más bajo del árbol). Null si no tiene.</summary>
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

        private static async Task<int?> ResolveWorkerIdAsync(AppDbContext ctx, int userId)
        {
            var id = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .Select(w => (int?)w.Id)
                .FirstOrDefaultAsync();
            return id;
        }

        private sealed class SalidaPropia
        {
            public int Id { get; init; }
            public string? Codigo { get; init; }
            public int RendicionId { get; init; }
            public DateOnly FechaSalida { get; init; }
            public int EstadoReembolsoId { get; init; }
            public string? ObservacionReembolso { get; init; }
            /// <summary>Jefatura o Tesoreria. Ver EstadosSalida.OrigenObservacionReembolso.</summary>
            public int? ObservacionReembolsoOrigenId { get; init; }
        }

        /// <summary>Salidas rendidas del trabajador que cuelgan de una planilla.</summary>
        private static async Task<List<SalidaPropia>> CargarSalidasPropiasAsync(
            AppDbContext ctx, int workerId, int? rendicionId = null)
        {
            var query = ctx.GaSolicitudSalida
                .Where(s => s.WorkerId == workerId && s.RendicionId != null);

            if (rendicionId.HasValue)
                query = query.Where(s => s.RendicionId == rendicionId.Value);

            return await query
                .Select(s => new SalidaPropia
                {
                    Id                   = s.Id,
                    Codigo               = s.Codigo,
                    RendicionId          = s.RendicionId!.Value,
                    FechaSalida          = s.FechaSalida,
                    EstadoReembolsoId    = s.EstadoReembolsoId,
                    ObservacionReembolso = s.ObservacionReembolso,
                    ObservacionReembolsoOrigenId = s.ObservacionReembolsoOrigenId,
                })
                .ToListAsync();
        }

        /// <summary>solicitudId → monto rendido, con la misma regla que imprime la planilla.</summary>
        private static async Task<Dictionary<int, decimal>> MontosPorSolicitudAsync(
            AppDbContext ctx, List<int> solicitudIds, int workerId)
        {
            if (solicitudIds.Count == 0) return new();

            var subarea = await ctx.Worker
                .Where(w => w.Id == workerId)
                .Select(w => w.Subarea)
                .FirstOrDefaultAsync();

            var trayectos = await ctx.GaSolicitudTrayecto
                .Where(t => solicitudIds.Contains(t.SolicitudId))
                .Select(t => new { t.Id, t.SolicitudId, t.LugarOrigenId, t.LugarDestinoId })
                .ToListAsync();

            var importes = await ImporteRendidoLoader.LoadAsync(
                ctx,
                trayectos
                    .Select(t => new ImporteRendidoLoader.TrayectoParaImporte(
                        t.Id, subarea, t.LugarOrigenId, t.LugarDestinoId))
                    .ToList());

            return trayectos
                .GroupBy(t => t.SolicitudId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(t => importes.TryGetValue(t.Id, out var imp) ? imp.Importe : 0m));
        }

        private sealed class TrayectoResumen
        {
            public int Id { get; init; }
            public int SolicitudId { get; init; }
            public int Orden { get; init; }
            public string Motivo { get; init; } = string.Empty;
            public string? LugarOrigen { get; init; }
            public string? LugarDestino { get; init; }
        }

        private static async Task<Dictionary<int, List<TrayectoResumen>>> CargarTrayectosAsync(
            AppDbContext ctx, List<int> solicitudIds)
        {
            if (solicitudIds.Count == 0) return new();

            var filas = await (
                from t  in ctx.GaSolicitudTrayecto
                join m  in ctx.GaMotivoSalida on t.MotivoId equals m.Id into mGroup
                from m  in mGroup.DefaultIfEmpty()
                join lo in ctx.GaLugar on t.LugarOrigenId equals lo.Id into loGroup
                from lo in loGroup.DefaultIfEmpty()
                join po in ctx.Project on lo.ProjectId equals (int?)po.ProjectId into poGroup
                from po in poGroup.DefaultIfEmpty()
                join ld in ctx.GaLugar on t.LugarDestinoId equals ld.Id into ldGroup
                from ld in ldGroup.DefaultIfEmpty()
                join pd in ctx.Project on ld.ProjectId equals (int?)pd.ProjectId into pdGroup
                from pd in pdGroup.DefaultIfEmpty()
                where solicitudIds.Contains(t.SolicitudId)
                orderby t.SolicitudId, t.Orden
                select new TrayectoResumen
                {
                    Id           = t.Id,
                    SolicitudId  = t.SolicitudId,
                    Orden        = t.Orden,
                    Motivo       = m == null || m.EsMotivoLibre ? (t.MotivoLibre ?? string.Empty) : m.Descripcion,
                    LugarOrigen  = lo == null ? t.LugarOrigenLibre
                                 : lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : "[Sin proyecto]")
                                 : lo.Nombre,
                    LugarDestino = ld == null ? t.LugarDestinoLibre
                                 : ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : "[Sin proyecto]")
                                 : ld.Nombre,
                }
            ).ToListAsync();

            // Esta pantalla muestra la PLANILLA, no la salida: se queda con los trayectos que se
            // rindieron. Los que no generan reembolso no están impresos en el PDF, así que anunciar
            // su recorrido acá haría dudar de un monto que no los incluye. La salida completa se
            // sigue viendo en su detalle.
            var rendibles = await ReembolsoTrayectoRule.CargarRendiblesAsync(
                ctx, filas.Select(t => t.Id).ToList());

            return filas
                .Where(t => rendibles.Contains(t.Id))
                .GroupBy(t => t.SolicitudId)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Orden).ToList());
        }


        private static RendicionListItemDto Armar(
            Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Models.GaRendicion planilla,
            List<SalidaPropia> propias,
            ConsolidadoS10Dto? consolidado,
            Dictionary<int, decimal> montos,
            decimal montoTotalPlanilla)
        {
            var desde = propias.Min(s => s.FechaSalida);
            var hasta = propias.Max(s => s.FechaSalida);

            var estado = PlanillaRendicionHelper.ResumirEstadoReembolso(propias.Select(s => s.EstadoReembolsoId));

            // La observación vigente de la planilla, con su origen (jefatura o Tesorería).
            var observada = propias.FirstOrDefault(
                s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Observado
                  && !string.IsNullOrWhiteSpace(s.ObservacionReembolso));

            return new RendicionListItemDto
            {
                Id             = planilla.Id,
                Codigo         = PlanillaRendicionHelper.CodigoRendicion(planilla.Codigo, planilla.Id),
                NumeroPlanilla = PlanillaRendicionHelper.NumeroPlanilla(planilla.NumeroPlanilla),
                RendidoAt      = planilla.RendidoAt,
                Periodo        = PlanillaRendicionHelper.EtiquetaPeriodo(desde, hasta),
                PeriodoAnio    = desde.Year,
                PeriodoMes     = desde.Month,
                SalidasCount   = propias.Count,
                MontoTotal     = propias.Sum(s => montos.TryGetValue(s.Id, out var m) ? m : 0m),
                MontoTotalPlanilla = montoTotalPlanilla,

                PdfUrl             = planilla.PdfUrl,
                PdfFilename        = planilla.PdfFilename,
                PdfFirmadoUrl      = planilla.PdfFirmadoUrl,
                PdfFirmadoFilename = planilla.PdfFirmadoFilename,
                FirmadoAt          = planilla.FirmadoAt,

                ConsolidadoS10 = consolidado,

                EstadoReembolso = estado,
                ReembolsoMixto  = propias.Select(s => s.EstadoReembolsoId).Distinct().Count() > 1,
                // El texto y su origen salen de la MISMA salida: si no, una planilla observada por
                // Tesorería podría mostrar el motivo de una y el rótulo de la otra.
                ObservacionReembolso       = observada?.ObservacionReembolso,
                ObservacionReembolsoOrigen = EstadosSalida.OrigenObservacionReembolso.Nombre(
                    observada?.ObservacionReembolsoOrigenId),

                EstadoPrimeraRevision      = EstadosSalida.PrimeraRevision.Nombre(planilla.EstadoPrimeraRevisionId),
                EnviadaRevisionAt          = planilla.EnviadaRevisionAt,
                PrimeraRevisionAt          = planilla.PrimeraRevisionAt,
                PrimeraRevisionObservacion = planilla.PrimeraRevisionObservacion,
                PuedeEnviarPrimeraRevision = planilla.EstadoPrimeraRevisionId == EstadosSalida.PrimeraRevision.Borrador,
                PuedeSubsanar              = planilla.EstadoPrimeraRevisionId == EstadosSalida.PrimeraRevision.Observada,
            };
        }

        private static void CopiarCabecera(RendicionListItemDto origen, RendicionDetalleDto destino)
        {
            destino.Id                       = origen.Id;
            destino.Codigo                   = origen.Codigo;
            destino.NumeroPlanilla           = origen.NumeroPlanilla;
            destino.RendidoAt                = origen.RendidoAt;
            destino.Periodo                  = origen.Periodo;
            destino.PeriodoAnio              = origen.PeriodoAnio;
            destino.PeriodoMes               = origen.PeriodoMes;
            destino.SalidasCount             = origen.SalidasCount;
            destino.MontoTotal               = origen.MontoTotal;
            destino.MontoTotalPlanilla       = origen.MontoTotalPlanilla;
            destino.PdfUrl                   = origen.PdfUrl;
            destino.PdfFilename              = origen.PdfFilename;
            destino.PdfFirmadoUrl            = origen.PdfFirmadoUrl;
            destino.PdfFirmadoFilename       = origen.PdfFirmadoFilename;
            destino.FirmadoAt                = origen.FirmadoAt;
            destino.ConsolidadoS10           = origen.ConsolidadoS10;
            destino.EstadoReembolso          = origen.EstadoReembolso;
            destino.ReembolsoMixto           = origen.ReembolsoMixto;
            destino.ObservacionReembolso     = origen.ObservacionReembolso;
            destino.ObservacionReembolsoOrigen = origen.ObservacionReembolsoOrigen;
            destino.EstadoPrimeraRevision      = origen.EstadoPrimeraRevision;
            destino.EnviadaRevisionAt          = origen.EnviadaRevisionAt;
            destino.PrimeraRevisionAt          = origen.PrimeraRevisionAt;
            destino.PrimeraRevisionObservacion = origen.PrimeraRevisionObservacion;
            destino.PuedeEnviarPrimeraRevision = origen.PuedeEnviarPrimeraRevision;
            destino.PuedeSubsanar              = origen.PuedeSubsanar;
        }

        private static List<RendicionListItemDto> Filtrar(
            List<RendicionListItemDto> items, RendicionFiltersDto? filters)
        {
            if (filters == null) return items;

            IEnumerable<RendicionListItemDto> q = items;

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
