using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Arma las filas de planilla que muestran Gestión de Rendiciones (el revisor) y Reembolsos
    /// (Tesorería). Las dos pantallas son la misma tabla vista desde distinto alcance —una decide
    /// el reembolso, la otra paga— así que comparten el armado para que no puedan discrepar en el
    /// estado, el periodo ni el monto de la misma planilla.
    ///
    /// Recibe la consulta de salidas YA recortada por visibilidad y por los filtros de la pantalla;
    /// acá solo se agrupa por planilla y se completan sus datos.
    /// </summary>
    public static class PlanillaRendicionLoader
    {
        /// <summary>Una salida dentro de la planilla, con lo que necesitan las dos pantallas.</summary>
        public sealed class SalidaFila
        {
            public int Id { get; init; }
            public string? Codigo { get; init; }
            public int WorkerId { get; init; }
            public string Trabajador { get; init; } = string.Empty;
            public string? Area { get; init; }
            public DateOnly FechaSalida { get; init; }
            public int EstadoReembolsoId { get; init; }
            public string EstadoReembolso => EstadosSalida.Reembolso.Nombre(EstadoReembolsoId);
            public string? ObservacionReembolso { get; init; }
            public DateTimeOffset? RevisorNotificadoAt { get; init; }
            public decimal Monto { get; set; }
            public string Motivo { get; set; } = string.Empty;
            public string? LugarOrigen { get; set; }
            public string? LugarDestino { get; set; }
            public int TrayectosCount { get; set; }
        }

        /// <summary>Una planilla con las salidas visibles que agrupa.</summary>
        public sealed class PlanillaFila
        {
            public int Id { get; init; }
            public string? NumeroPlanilla { get; init; }
            public DateTimeOffset RendidoAt { get; init; }
            public string PdfUrl { get; init; } = string.Empty;
            public string PdfFilename { get; init; } = string.Empty;
            public string? PdfFirmadoUrl { get; init; }
            public string? PdfFirmadoFilename { get; init; }
            public DateTimeOffset? FirmadoAt { get; init; }
            public ConsolidadoS10Dto? ConsolidadoS10 { get; init; }

            public List<SalidaFila> Salidas { get; init; } = new();

            public string Periodo { get; init; } = string.Empty;
            public int PeriodoAnio { get; init; }
            public int PeriodoMes { get; init; }
            public string EstadoReembolso { get; init; } = string.Empty;
            public bool ReembolsoMixto { get; init; }
            public string? ObservacionReembolso { get; init; }
            public DateTimeOffset? RevisorNotificadoAt { get; init; }

            /// <summary>Nombres de los trabajadores que aparecen en la planilla, sin repetir.</summary>
            public List<string> Trabajadores { get; init; } = new();
            public decimal MontoTotal { get; init; }
            public int SalidasCount => Salidas.Count;
        }

        /// <param name="salidasVisibles">
        /// Salidas ya recortadas por visibilidad y filtros. Se ignoran las que no están rendidas:
        /// sin planilla no hay fila que mostrar.
        /// </param>
        /// <param name="conDetalle">
        /// true = además resuelve motivo, origen y destino de cada salida (para el modal de
        /// detalle). En el listado se omite: son cuatro joins que la tabla no usa.
        /// </param>
        public static async Task<List<PlanillaFila>> LoadAsync(
            AppDbContext ctx,
            IQueryable<GaSolicitudSalida> salidasVisibles,
            bool conDetalle = false)
        {
            var salidas = await (
                from s in salidasVisibles.Where(x => x.RendicionId != null)
                join w in ctx.Worker on s.WorkerId equals w.Id
                join per in ctx.Person on w.PersonId equals (int?)per.PersonId into perGroup
                from per in perGroup.DefaultIfEmpty()
                select new
                {
                    s.Id,
                    s.Codigo,
                    RendicionId = s.RendicionId!.Value,
                    WorkerId    = w.Id,
                    w.Subarea,
                    Trabajador  = per != null ? (per.FullName ?? "Trabajador") : "Trabajador",
                    Area        = w.Area,
                    s.FechaSalida,
                    s.EstadoReembolsoId,
                    s.ObservacionReembolso,
                    s.RevisorNotificadoAt,
                }
            ).ToListAsync();

            if (salidas.Count == 0) return new();

            var rendicionIds = salidas.Select(x => x.RendicionId).Distinct().ToList();
            var solicitudIds = salidas.Select(x => x.Id).ToList();

            var planillas = await ctx.GaRendicion
                .Where(r => rendicionIds.Contains(r.Id))
                .ToListAsync();

            var consolidados = await ConsolidadoS10Loader.LoadPorRendicionAsync(ctx, rendicionIds);

            // Monto por salida, con la misma regla que imprime la columna IMPORTE de la planilla.
            var trayectos = await ctx.GaSolicitudTrayecto
                .Where(t => solicitudIds.Contains(t.SolicitudId))
                .Select(t => new { t.Id, t.SolicitudId, t.Orden, t.LugarOrigenId, t.LugarDestinoId })
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
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(t => importes.TryGetValue(t.Id, out var imp) ? imp.Importe : 0m));

            var trayectosPorSolicitud = trayectos
                .GroupBy(t => t.SolicitudId)
                .ToDictionary(g => g.Key, g => g.Count());

            var detalle = conDetalle
                ? await CargarDetalleTrayectosAsync(ctx, solicitudIds)
                : new Dictionary<int, (string Motivo, string? Origen, string? Destino)>();

            var porRendicion = salidas.GroupBy(x => x.RendicionId).ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<PlanillaFila>(planillas.Count);
            foreach (var planilla in planillas)
            {
                if (!porRendicion.TryGetValue(planilla.Id, out var visibles) || visibles.Count == 0) continue;

                var filas = visibles
                    .OrderBy(x => x.Trabajador).ThenBy(x => x.FechaSalida).ThenBy(x => x.Id)
                    .Select(x =>
                    {
                        detalle.TryGetValue(x.Id, out var det);
                        return new SalidaFila
                        {
                            Id                   = x.Id,
                            Codigo               = x.Codigo,
                            WorkerId             = x.WorkerId,
                            Trabajador           = x.Trabajador,
                            Area                 = x.Area,
                            FechaSalida          = x.FechaSalida,
                            EstadoReembolsoId    = x.EstadoReembolsoId,
                            ObservacionReembolso = x.ObservacionReembolso,
                            RevisorNotificadoAt  = x.RevisorNotificadoAt,
                            Monto                = montoPorSolicitud.TryGetValue(x.Id, out var m) ? m : 0m,
                            Motivo               = det.Motivo ?? string.Empty,
                            LugarOrigen          = det.Origen,
                            LugarDestino         = det.Destino,
                            TrayectosCount       = trayectosPorSolicitud.TryGetValue(x.Id, out var tc) ? tc : 0,
                        };
                    })
                    .ToList();

                var desde  = filas.Min(f => f.FechaSalida);
                var hasta  = filas.Max(f => f.FechaSalida);
                var estado = PlanillaRendicionHelper.ResumirEstadoReembolso(filas.Select(f => f.EstadoReembolsoId));

                consolidados.TryGetValue(planilla.Id, out var consolidado);

                result.Add(new PlanillaFila
                {
                    Id                 = planilla.Id,
                    NumeroPlanilla     = PlanillaRendicionHelper.NumeroPlanilla(planilla.NumeroPlanilla),
                    RendidoAt          = planilla.RendidoAt,
                    PdfUrl             = planilla.PdfUrl,
                    PdfFilename        = planilla.PdfFilename,
                    PdfFirmadoUrl      = planilla.PdfFirmadoUrl,
                    PdfFirmadoFilename = planilla.PdfFirmadoFilename,
                    FirmadoAt          = planilla.FirmadoAt,
                    ConsolidadoS10     = consolidado,
                    Salidas            = filas,
                    Periodo            = PlanillaRendicionHelper.EtiquetaPeriodo(desde, hasta),
                    PeriodoAnio        = desde.Year,
                    PeriodoMes         = desde.Month,
                    EstadoReembolso    = estado,
                    ReembolsoMixto     = filas.Select(f => f.EstadoReembolsoId).Distinct().Count() > 1,
                    ObservacionReembolso = filas
                        .Where(f => f.EstadoReembolsoId == EstadosSalida.Reembolso.Rechazado
                                 && !string.IsNullOrWhiteSpace(f.ObservacionReembolso))
                        .Select(f => f.ObservacionReembolso)
                        .FirstOrDefault(),
                    RevisorNotificadoAt = filas.Max(f => f.RevisorNotificadoAt),
                    Trabajadores        = filas.Select(f => f.Trabajador).Distinct().ToList(),
                    MontoTotal          = filas.Sum(f => f.Monto),
                });
            }

            // Más reciente primero: lo último rendido es lo que tiene pasos pendientes.
            return result.OrderByDescending(r => r.RendidoAt).ToList();
        }

        /// <summary>Motivo, origen y destino de cada salida (primer y último trayecto).</summary>
        private static async Task<Dictionary<int, (string Motivo, string? Origen, string? Destino)>>
            CargarDetalleTrayectosAsync(AppDbContext ctx, List<int> solicitudIds)
        {
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
                select new
                {
                    t.SolicitudId,
                    t.Orden,
                    Motivo       = m != null ? m.Descripcion : (t.MotivoLibre ?? string.Empty),
                    LugarOrigen  = lo == null ? t.LugarOrigenLibre
                                 : lo.Tipo == "proyecto" ? (po != null ? po.ProjectDescription : "[Sin proyecto]")
                                 : lo.Nombre,
                    LugarDestino = ld == null ? t.LugarDestinoLibre
                                 : ld.Tipo == "proyecto" ? (pd != null ? pd.ProjectDescription : "[Sin proyecto]")
                                 : ld.Nombre,
                }
            ).ToListAsync();

            return filas
                .GroupBy(t => t.SolicitudId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var ordenados = g.OrderBy(x => x.Orden).ToList();
                        return (ordenados[0].Motivo, ordenados[0].LugarOrigen, ordenados[^1].LugarDestino);
                    });
        }
    }
}
