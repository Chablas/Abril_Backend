using Dapper;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

/// <summary>Recalcula ss_presupuesto.total_estimado como la suma real de las 5 fuentes de costo de
/// un presupuesto: materiales por ratio (ss_presupuesto_detalle), dotación de personal
/// (ss_presupuesto_personal_hito), vigilancia (ss_presupuesto_vigilancia_hito), servicios de costo
/// fijo (ss_presupuesto_item_metrado) y kits/BOM (ss_presupuesto_kit_item). Se llama después de
/// cualquier operación que inserte, borre o edite filas en esas tablas — sin esto, guardar
/// Personal/Vigilancia/Servicios/Kits dejaba el total desactualizado (solo reflejaba materiales), y
/// con eso tanto la grilla de versiones como el dashboard comparativo mostraban un presupuesto
/// incompleto.</summary>
public static class PresupuestoTotalHelper
{
    public static Task RecalcularTotalAsync(NpgsqlConnection conn, int presupuestoId, NpgsqlTransaction? tx = null) =>
        conn.ExecuteAsync(
            """
            UPDATE ss_presupuesto p
            SET total_estimado =
                COALESCE((
                    SELECT SUM(COALESCE(d.cantidad_manual, d.cantidad_estimada) * COALESCE(d.precio_manual, d.precio_unitario))
                    FROM ss_presupuesto_detalle d WHERE d.presupuesto_id = p.id
                ), 0)
              + COALESCE((SELECT SUM(ph.total) FROM ss_presupuesto_personal_hito ph WHERE ph.presupuesto_id = p.id), 0)
              + COALESCE((SELECT SUM(vh.total) FROM ss_presupuesto_vigilancia_hito vh WHERE vh.presupuesto_id = p.id), 0)
              + COALESCE((SELECT SUM(im.total) FROM ss_presupuesto_item_metrado im WHERE im.presupuesto_id = p.id), 0)
              + COALESCE((SELECT SUM(ki.total) FROM ss_presupuesto_kit_item ki WHERE ki.presupuesto_id = p.id), 0)
            WHERE p.id = @presupuestoId
            """,
            new { presupuestoId }, tx);
}
