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

            var porRendicion = salidas.GroupBy(s => s.RendicionId).ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<RendicionListItemDto>(planillas.Count);
            foreach (var planilla in planillas)
            {
                if (!porRendicion.TryGetValue(planilla.Id, out var propias) || propias.Count == 0) continue;

                consolidados.TryGetValue(planilla.Id, out var consolidado);
                result.Add(Armar(planilla, propias, consolidado, montos));
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

            var cabecera = Armar(planilla, propias, consolidado, montos);
            var detalle  = new RendicionDetalleDto();
            CopiarCabecera(cabecera, detalle);

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

        public async Task<List<PeriodoOptionDto>> GetPeriodos(int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var workerId = await ResolveWorkerIdAsync(ctx, userId);
            if (workerId == null) return new();

            var salidas = await CargarSalidasPropiasAsync(ctx, workerId.Value);
            if (salidas.Count == 0) return new();

            // El periodo de una planilla es el mes de su salida más antigua — el mismo criterio
            // que usa la tabla, si no el filtro dejaría fuera planillas que sí muestra.
            return salidas
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
        }

        public async Task MarcarRevisorNotificado(int rendicionId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var workerId = await ResolveWorkerIdAsync(ctx, userId);
            if (workerId == null) return;

            var now = DateTimeOffset.UtcNow;
            var propias = await ctx.GaSolicitudSalida
                .Where(s => s.RendicionId == rendicionId && s.WorkerId == workerId.Value)
                .ToListAsync();

            foreach (var s in propias)
            {
                s.RevisorNotificadoAt    = now;
                s.RevisorNotificadoPorId = userId;
                s.UpdatedAt              = now;
            }
            await ctx.SaveChangesAsync();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

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
            public DateTimeOffset? RevisorNotificadoAt { get; init; }
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
                    RevisorNotificadoAt  = s.RevisorNotificadoAt,
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
                    SolicitudId  = t.SolicitudId,
                    Orden        = t.Orden,
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
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Orden).ToList());
        }

        private static RendicionListItemDto Armar(
            Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Infrastructure.Models.GaRendicion planilla,
            List<SalidaPropia> propias,
            ConsolidadoS10Dto? consolidado,
            Dictionary<int, decimal> montos)
        {
            var desde = propias.Min(s => s.FechaSalida);
            var hasta = propias.Max(s => s.FechaSalida);

            var estado = PlanillaRendicionHelper.ResumirEstadoReembolso(propias.Select(s => s.EstadoReembolsoId));
            var abierto = estado == EstadosSalida.Reembolso.NombrePendiente
                       || estado == EstadosSalida.Reembolso.NombreRechazado;

            return new RendicionListItemDto
            {
                Id             = planilla.Id,
                NumeroPlanilla = PlanillaRendicionHelper.NumeroPlanilla(planilla.NumeroPlanilla),
                RendidoAt      = planilla.RendidoAt,
                Periodo        = PlanillaRendicionHelper.EtiquetaPeriodo(desde, hasta),
                PeriodoAnio    = desde.Year,
                PeriodoMes     = desde.Month,
                SalidasCount   = propias.Count,
                MontoTotal     = propias.Sum(s => montos.TryGetValue(s.Id, out var m) ? m : 0m),

                PdfUrl             = planilla.PdfUrl,
                PdfFilename        = planilla.PdfFilename,
                PdfFirmadoUrl      = planilla.PdfFirmadoUrl,
                PdfFirmadoFilename = planilla.PdfFirmadoFilename,
                FirmadoAt          = planilla.FirmadoAt,

                ConsolidadoS10 = consolidado,

                EstadoReembolso = estado,
                ReembolsoMixto  = propias.Select(s => s.EstadoReembolsoId).Distinct().Count() > 1,
                ObservacionReembolso = propias
                    .Where(s => s.EstadoReembolsoId == EstadosSalida.Reembolso.Rechazado
                             && !string.IsNullOrWhiteSpace(s.ObservacionReembolso))
                    .Select(s => s.ObservacionReembolso)
                    .FirstOrDefault(),
                RevisorNotificadoAt = propias.Max(s => s.RevisorNotificadoAt),

                PuedeAdjuntarConsolidado = abierto,
                PuedeNotificarRevisor    = abierto && consolidado != null,
            };
        }

        private static void CopiarCabecera(RendicionListItemDto origen, RendicionDetalleDto destino)
        {
            destino.Id                       = origen.Id;
            destino.NumeroPlanilla           = origen.NumeroPlanilla;
            destino.RendidoAt                = origen.RendidoAt;
            destino.Periodo                  = origen.Periodo;
            destino.PeriodoAnio              = origen.PeriodoAnio;
            destino.PeriodoMes               = origen.PeriodoMes;
            destino.SalidasCount             = origen.SalidasCount;
            destino.MontoTotal               = origen.MontoTotal;
            destino.PdfUrl                   = origen.PdfUrl;
            destino.PdfFilename              = origen.PdfFilename;
            destino.PdfFirmadoUrl            = origen.PdfFirmadoUrl;
            destino.PdfFirmadoFilename       = origen.PdfFirmadoFilename;
            destino.FirmadoAt                = origen.FirmadoAt;
            destino.ConsolidadoS10           = origen.ConsolidadoS10;
            destino.EstadoReembolso          = origen.EstadoReembolso;
            destino.ReembolsoMixto           = origen.ReembolsoMixto;
            destino.ObservacionReembolso     = origen.ObservacionReembolso;
            destino.RevisorNotificadoAt      = origen.RevisorNotificadoAt;
            destino.PuedeAdjuntarConsolidado = origen.PuedeAdjuntarConsolidado;
            destino.PuedeNotificarRevisor    = origen.PuedeNotificarRevisor;
        }

        private static List<RendicionListItemDto> Filtrar(
            List<RendicionListItemDto> items, RendicionFiltersDto? filters)
        {
            if (filters == null) return items;

            IEnumerable<RendicionListItemDto> q = items;

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
