using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// La regla de reembolso de un trayecto en un solo lugar. Es asimétrica:
    /// <list type="number">
    ///   <item>Lo <b>concede</b> el motivo del catálogo (<c>ga_motivo_salida.es_reembolsable</c>,
    ///   Configuración → Motivos), que arranca en false.</item>
    ///   <item>El par (origen, destino) elegido solo puede <b>anularlo</b>
    ///   (<c>ga_trayecto.es_reembolsable = false</c>), nunca al revés.</item>
    /// </list>
    /// Extraída acá porque la aplican los detalles de Solicitud de Salidas y de Gestión de
    /// Salidas para pintar el pill del trayecto: son features distintas del mismo módulo y no
    /// pueden mostrar respuestas distintas del mismo gasto.
    /// </summary>
    public static class ReembolsoTrayectoRule
    {
        /// <summary>
        /// Pares (origen, destino) del catálogo activo marcados como NO reembolsables: las
        /// excepciones que anulan el reembolso que concede el motivo. Es una lista corta y global
        /// (no depende del trabajador), así que se trae entera en 1 consulta.
        /// </summary>
        public static async Task<HashSet<(int Origen, int Destino)>> CargarExcluidosAsync(AppDbContext ctx)
        {
            var pares = await ctx.GaTrayecto
                .Where(t => t.Activo && !t.EsReembolsable)
                .Select(t => new { t.LugarOrigenId, t.LugarDestinoId })
                .ToListAsync();

            return pares.Select(p => (p.LugarOrigenId, p.LugarDestinoId)).ToHashSet();
        }

        /// <summary>
        /// Si un trayecto genera reembolso de movilidad. Devuelve <c>null</c> cuando el motivo es
        /// libre (<c>motivo_id</c> NULL): ese no está en el catálogo, así que no tiene el flag
        /// configurado y no hay nada que afirmar — quien lo muestre debe omitir el dato en vez de
        /// inventar un "no reembolsable" que nadie configuró.
        /// </summary>
        /// <param name="esMotivoDeCatalogo">false = motivo libre escrito por el trabajador.</param>
        /// <param name="motivoEsReembolsable">El flag del motivo del catálogo.</param>
        public static bool? Resolver(
            bool esMotivoDeCatalogo,
            bool motivoEsReembolsable,
            int? lugarOrigenId,
            int? lugarDestinoId,
            HashSet<(int Origen, int Destino)> excluidos)
        {
            if (!esMotivoDeCatalogo) return null;
            if (!motivoEsReembolsable) return false;

            // Los motivos que no piden lugares (pide_horas_lugares = false) no tienen par que
            // excluir: si el motivo concede, el trayecto queda reembolsable.
            if (lugarOrigenId is null || lugarDestinoId is null) return true;

            return !excluidos.Contains((lugarOrigenId.Value, lugarDestinoId.Value));
        }
    }
}
