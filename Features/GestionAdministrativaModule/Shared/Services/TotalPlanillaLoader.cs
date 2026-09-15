using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Monto total de una planilla de rendición: la suma de TODAS sus salidas, sin recortar por
    /// visibilidad ni por dueño.
    ///
    /// Va aparte del monto que muestran las pantallas justamente por eso. Mis Rendiciones suma
    /// solo las salidas propias y Gestión de Rendiciones solo las visibles —una planilla generada
    /// por el revisor puede agrupar a varios trabajadores—, pero el Consolidado del S10 cubre la
    /// planilla entera: es UN registro en el S10. El importe contra el que se contrasta el
    /// consolidado tiene que ser el de la planilla completa, no el del pedacito que cada quien ve.
    ///
    /// El importe por trayecto sale de <see cref="ImporteRendidoLoader"/>, la misma regla que
    /// imprime la columna IMPORTE de la planilla: así el número que se le exige al trabajador es
    /// exactamente el que dice el PDF que tiene delante.
    /// </summary>
    public static class TotalPlanillaLoader
    {
        /// <summary>rendicionId → monto total de la planilla. Trae 0 para las que no tienen salidas.</summary>
        public static async Task<Dictionary<int, decimal>> LoadAsync(
            AppDbContext ctx, IReadOnlyCollection<int> rendicionIds)
        {
            var ids = rendicionIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            // Todas las salidas de esas planillas, con la subárea de su trabajador: la regla del
            // importe la necesita para los de TI, que rinden contra el catálogo de trayectos.
            var salidas = await (
                from s in ctx.GaSolicitudSalida
                join w in ctx.Worker on s.WorkerId equals w.Id
                where s.RendicionId != null && ids.Contains(s.RendicionId.Value)
                select new { s.Id, RendicionId = s.RendicionId!.Value, w.Subarea }
            ).ToListAsync();

            var total = ids.ToDictionary(id => id, _ => 0m);
            if (salidas.Count == 0) return total;

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

            var rendicionPorSolicitud = salidas.ToDictionary(s => s.Id, s => s.RendicionId);

            foreach (var t in trayectos)
            {
                if (!rendicionPorSolicitud.TryGetValue(t.SolicitudId, out var rendicionId)) continue;
                if (!importes.TryGetValue(t.Id, out var imp)) continue;
                total[rendicionId] = total[rendicionId] + imp.Importe;
            }

            return total;
        }

        /// <summary>Monto total de UNA planilla. 0 si no tiene salidas con importe.</summary>
        public static async Task<decimal> LoadOneAsync(AppDbContext ctx, int rendicionId)
        {
            var totales = await LoadAsync(ctx, new[] { rendicionId });
            return totales.TryGetValue(rendicionId, out var monto) ? monto : 0m;
        }
    }
}
