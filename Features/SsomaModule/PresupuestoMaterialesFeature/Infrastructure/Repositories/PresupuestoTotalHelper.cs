using Dapper;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

/// <summary>Recalcula ss_presupuesto.total_estimado como la suma real de las fuentes de costo
/// OFICIALES de un presupuesto: materiales por ratio (ss_presupuesto_detalle), dotación de personal
/// (ss_presupuesto_personal_hito), servicios de costo fijo (ss_presupuesto_item_metrado) y kits/BOM
/// (ss_presupuesto_kit_item). Se llama después de cualquier operación que inserte, borre o edite
/// filas en esas tablas — sin esto, guardar Personal/Servicios/Kits dejaba el total desactualizado
/// (solo reflejaba materiales), y con eso tanto la grilla de versiones como el dashboard comparativo
/// mostraban un presupuesto incompleto.
/// Vigilancia (ss_presupuesto_vigilancia_hito) NO entra en este total a propósito — no es parte del
/// presupuesto oficial de Costos, se sigue guardando y mostrando en su propia pestaña solo como
/// referencia, pero ya no suma acá.</summary>
public static class PresupuestoTotalHelper
{
    public static Task RecalcularTotalAsync(NpgsqlConnection conn, int presupuestoId, NpgsqlTransaction? tx = null) =>
        conn.ExecuteAsync(
            """
            UPDATE ss_presupuesto p
            SET total_estimado = ROUND(
                COALESCE((
                    SELECT SUM(ROUND(COALESCE(d.cantidad_manual, d.cantidad_estimada) * COALESCE(d.precio_manual, d.precio_unitario), 2))
                    FROM ss_presupuesto_detalle d WHERE d.presupuesto_id = p.id
                ), 0)
              + COALESCE((SELECT SUM(ROUND(ph.total, 2)) FROM ss_presupuesto_personal_hito ph WHERE ph.presupuesto_id = p.id), 0)
              + COALESCE((SELECT SUM(ROUND(im.total, 2)) FROM ss_presupuesto_item_metrado im WHERE im.presupuesto_id = p.id), 0)
              + COALESCE((SELECT SUM(ROUND(ki.total, 2)) FROM ss_presupuesto_kit_item ki WHERE ki.presupuesto_id = p.id), 0)
            , 2)
            WHERE p.id = @presupuestoId
            """,
            new { presupuestoId }, tx);
}
