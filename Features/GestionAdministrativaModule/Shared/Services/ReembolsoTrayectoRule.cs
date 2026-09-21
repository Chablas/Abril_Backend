using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Services
{
    /// <summary>
    /// La regla de reembolso de un trayecto en un solo lugar. Es asimétrica:
    /// <list type="number">
    ///   <item>Lo <b>concede</b> el motivo (<c>ga_motivo_salida.es_reembolsable</c>,
    ///   Configuración → Motivos), que arranca en false. Incluye a "Otro motivo": el texto libre
    ///   apunta a su propia fila del catálogo (<c>es_motivo_libre</c>), que lleva el mismo flag.</item>
    ///   <item>El par (origen, destino) elegido solo puede <b>anularlo</b>
    ///   (<c>ga_trayecto.es_reembolsable = false</c>), nunca al revés.</item>
    /// </list>
    /// Extraída acá porque la aplican los detalles de Solicitud de Salidas y de Gestión de
    /// Salidas para pintar el pill del trayecto: son features distintas del mismo módulo y no
    /// pueden mostrar respuestas distintas del mismo gasto.
    ///
    /// La misma regla decide qué entra en la RENDICIÓN: un trayecto sin reembolso no genera gasto
    /// de movilidad, así que no se le exigen capturas, no sale impreso en la planilla y no suma a
    /// su monto (ver <see cref="CargarRendiblesAsync"/>).
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
        /// Si un trayecto genera reembolso de movilidad. Devuelve <c>null</c> solo cuando el
        /// trayecto no apunta a ninguna fila del catálogo (<c>motivo_id</c> NULL): son las
        /// solicitudes anteriores a la fila que configura "Otro motivo", donde el flag no existe y
        /// no hay nada que afirmar — quien lo muestre debe omitir el dato en vez de inventar un
        /// "no reembolsable" que nadie configuró.
        /// </summary>
        /// <param name="esMotivoDeCatalogo">false = trayecto sin fila de motivo (histórico).</param>
        /// <param name="motivoEsReembolsable">El flag del motivo, sea del desplegable o "Otro motivo".</param>
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

        /// <summary>
        /// De los trayectos pedidos, cuáles SÍ generan reembolso — o sea, los únicos que entran en
        /// la rendición: se les exige captura, se imprimen en la planilla y su importe suma al
        /// total que se contrasta contra el Consolidado del S10. Los que quedan fuera (motivo no
        /// reembolsable, motivo libre, o par origen-destino excluido del catálogo) se siguen viendo
        /// en el detalle de la salida con su pill SIN REEMBOLSO, pero no son gasto que rendir.
        /// "Otro motivo" ya no queda fuera por definición: entra o no según el flag de su fila.
        ///
        /// Son dos consultas fijas —los pares excluidos y el motivo de cada trayecto—, no una por
        /// trayecto: la pide en lote quien arma la planilla o decide si una salida es apta.
        /// </summary>
        public static async Task<HashSet<int>> CargarRendiblesAsync(
            AppDbContext ctx, IReadOnlyCollection<int> trayectoIds)
        {
            var ids = trayectoIds.Distinct().ToList();
            if (ids.Count == 0) return new();

            var excluidos = await CargarExcluidosAsync(ctx);

            var trayectos = await (
                from t in ctx.GaSolicitudTrayecto
                join m in ctx.GaMotivoSalida on t.MotivoId equals m.Id into mGroup
                from m in mGroup.DefaultIfEmpty()
                where ids.Contains(t.Id)
                select new
                {
                    t.Id,
                    // Sin fila de motivo (histórico) no hay flag configurado: por eso se distingue
                    // del motivo que lo tiene en false.
                    EsMotivoDeCatalogo   = m != null,
                    MotivoEsReembolsable = m != null && m.EsReembolsable,
                    t.LugarOrigenId,
                    t.LugarDestinoId,
                }
            ).ToListAsync();

            return trayectos
                .Where(t => Resolver(
                    t.EsMotivoDeCatalogo, t.MotivoEsReembolsable,
                    t.LugarOrigenId, t.LugarDestinoId, excluidos) == true)
                .Select(t => t.Id)
                .ToHashSet();
        }
    }
}
