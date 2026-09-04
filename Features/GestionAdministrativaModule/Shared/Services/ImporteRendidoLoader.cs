using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// Resuelve en lote cuánto se rindió por trayecto. Es la MISMA regla que imprime la columna
    /// IMPORTE de la planilla, extraída acá para que la planilla y la pantalla de Mis Rendiciones
    /// no puedan mostrar montos distintos del mismo gasto.
    ///
    /// Orden de precedencia:
    /// <list type="number">
    ///   <item>La suma de las capturas del trayecto, si hay alguna con monto.</item>
    ///   <item>El monto del catálogo <c>ga_trayecto</c> para ese par origen-destino, y solo si el
    ///   trabajador es de TI: es la única subárea que rinde contra tarifario en vez de capturas.</item>
    ///   <item>0 si no aplica ninguna de las dos.</item>
    /// </list>
    /// </summary>
    public static class ImporteRendidoLoader
    {
        /// <summary>
        /// Subárea que rinde contra el catálogo de trayectos en vez de capturas. Los repositorios
        /// de salidas tienen su propia copia de esta constante para decidir si un trayecto está
        /// CUBIERTO (otra pregunta distinta de cuánto vale); si cambia, hay que tocar las tres.
        /// </summary>
        public const string SubareaTi = "Tecnología de la Información";

        /// <summary>Un trayecto con lo justo para resolver su importe.</summary>
        public sealed record TrayectoParaImporte(
            int TrayectoId,
            string? Subarea,
            int? LugarOrigenId,
            int? LugarDestinoId);

        /// <summary>Importe resuelto de un trayecto y de dónde salió.</summary>
        public readonly record struct ImporteResuelto(decimal Importe, bool EsCatalogo);

        /// <summary>trayectoId → (importe, esCatalogo). Trae todos los trayectos pedidos.</summary>
        public static async Task<Dictionary<int, ImporteResuelto>> LoadAsync(
            AppDbContext ctx,
            IReadOnlyCollection<TrayectoParaImporte> trayectos)
        {
            var result = new Dictionary<int, ImporteResuelto>(trayectos.Count);
            if (trayectos.Count == 0) return result;

            var trayectoIds = trayectos.Select(t => t.TrayectoId).Distinct().ToList();
            var importesCapturas = await ctx.GaSolicitudCaptura
                .Where(c => trayectoIds.Contains(c.TrayectoId))
                .GroupBy(c => c.TrayectoId)
                .Select(g => new { TrayectoId = g.Key, Total = g.Sum(x => x.Monto) })
                .ToDictionaryAsync(x => x.TrayectoId, x => x.Total);

            // El catálogo solo se carga si hay algún trayecto de TI sin capturas que lo necesite.
            var necesitaCatalogo = trayectos.Any(t =>
                EsTi(t.Subarea) && !importesCapturas.ContainsKey(t.TrayectoId));
            var catalogoMap = necesitaCatalogo ? await CargarCatalogoAsync(ctx) : new();

            foreach (var t in trayectos)
            {
                if (importesCapturas.TryGetValue(t.TrayectoId, out var sumCap) && sumCap > 0m)
                {
                    result[t.TrayectoId] = new ImporteResuelto(sumCap, false);
                }
                else if (EsTi(t.Subarea)
                      && t.LugarOrigenId.HasValue && t.LugarDestinoId.HasValue
                      && catalogoMap.TryGetValue((t.LugarOrigenId.Value, t.LugarDestinoId.Value), out var montoCat))
                {
                    result[t.TrayectoId] = new ImporteResuelto(montoCat, true);
                }
                else
                {
                    result[t.TrayectoId] = new ImporteResuelto(0m, false);
                }
            }

            return result;
        }

        private static bool EsTi(string? subarea) =>
            string.Equals(subarea, SubareaTi, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Catálogo de trayectos activos en memoria. Llave: (lugar_origen_id, lugar_destino_id).
        /// </summary>
        private static async Task<Dictionary<(int, int), decimal>> CargarCatalogoAsync(AppDbContext ctx)
        {
            var rows = await ctx.GaTrayecto
                .Where(g => g.Activo)
                .Select(g => new { g.LugarOrigenId, g.LugarDestinoId, g.Monto })
                .ToListAsync();
            return rows.ToDictionary(r => (r.LugarOrigenId, r.LugarDestinoId), r => r.Monto);
        }
    }
}
