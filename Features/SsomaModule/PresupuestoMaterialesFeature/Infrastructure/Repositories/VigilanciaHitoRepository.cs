using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Dapper;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

/// <summary>Nombre de la família de Catálogo cuyo ratio de precio alimenta el costo del servicio de
/// vigilancia externa — el mismo mecanismo de Ratios que usan los demás materiales, no un costo tipeado
/// a mano por fila (a diferencia de "S/ mes" en Dotación de personal).</summary>
public class VigilanciaHitoRepository : IVigilanciaHitoRepository
{
    private const string FamiliaVigilancia = "Servicio de Vigilancia";

    private readonly IConfiguration _config;
    public VigilanciaHitoRepository(IConfiguration config) => _config = config;
    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]!);

    // Cronograma vigente de un proyecto: la versión de historial marcada como la actual.
    private const string CronogramaVigenteCte = """
        cronograma_vigente AS (
            SELECT ms.milestone_schedule_id, ms.milestone_id, ms.custom_description,
                   ms.planned_start_date, ms.es_hito_critico
            FROM milestone_schedule ms
            JOIN milestone_schedule_history msh
              ON msh.milestone_schedule_history_id = ms.milestone_schedule_history_id
            WHERE msh.project_id = @projectId
              AND msh.is_equal_to_last_version = true
              AND msh.active = true
              AND ms.active = true
              AND ms.es_hito_critico = true
        )
        """;

    private class FechaHitoRow
    {
        public int HitoId { get; set; }
        public DateOnly? Fecha { get; set; }
    }

    public async Task<decimal?> ObtenerPrecioUnitarioActualAsync()
    {
        using var conn = Conn();
        const string sql = """
            SELECT AVG(r.precio_unitario_promedio)
            FROM ss_ratio_proyecto r
            JOIN ss_material_familia f ON f.id = r.familia_id
            WHERE f.nombre = @familia AND r.incluido_manual_precio = true
            """;
        return await conn.QuerySingleOrDefaultAsync<decimal?>(sql, new { familia = FamiliaVigilancia });
    }

    public async Task<List<VigilanciaHitoDto>> ObtenerPorProyectoAsync(int projectId)
    {
        using var conn = Conn();
        var sql = $"""
            WITH {CronogramaVigenteCte}
            SELECT vh.id AS Id, vh.hito_id AS HitoId,
                   COALESCE(m.milestone_description, cv.custom_description, 'Hito') AS HitoDescripcion,
                   cv.planned_start_date AS HitoFecha,
                   cv.es_hito_critico AS EsHitoCritico,
                   vh.hito_salida_id AS HitoSalidaId,
                   COALESCE(m2.milestone_description, cv2.custom_description) AS HitoSalidaDescripcion,
                   cv2.planned_start_date AS HitoSalidaFecha,
                   vh.cantidad_puntos AS CantidadPuntos,
                   CASE
                       WHEN vh.hito_salida_id IS NOT NULL
                            AND cv.planned_start_date IS NOT NULL
                            AND cv2.planned_start_date IS NOT NULL
                       THEN GREATEST(0, (cv2.planned_start_date - cv.planned_start_date) / 7.0)
                       ELSE vh.semanas
                   END AS Semanas,
                   vh.precio_unitario AS PrecioUnitario, vh.total AS Total
            FROM ss_presupuesto_vigilancia_hito vh
            JOIN ss_presupuesto p ON p.id = vh.presupuesto_id
            JOIN cronograma_vigente cv ON cv.milestone_schedule_id = vh.hito_id
            LEFT JOIN cronograma_vigente cv2 ON cv2.milestone_schedule_id = vh.hito_salida_id
            LEFT JOIN milestone m ON m.milestone_id = cv.milestone_id
            LEFT JOIN milestone m2 ON m2.milestone_id = cv2.milestone_id
            WHERE p.project_id = @projectId
            ORDER BY cv.planned_start_date NULLS LAST
            """;
        var result = await conn.QueryAsync<VigilanciaHitoDto>(sql, new { projectId });
        return result.ToList();
    }

    public async Task GuardarAsync(int projectId, List<VigilanciaHitoItemInputDto> items, int userId)
    {
        using var conn = Conn();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        var presupuestoId = await conn.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT id FROM ss_presupuesto WHERE project_id = @projectId
            ORDER BY generado_en DESC LIMIT 1
            """, new { projectId }, tx);

        if (presupuestoId == null)
        {
            presupuestoId = await conn.QuerySingleAsync<int>(
                """
                INSERT INTO ss_presupuesto (project_id, version, estado, hh_usado, area_usada,
                    trabajadores_usados, total_estimado, generado_por, generado_en, notas)
                VALUES (@projectId, 1, 'BORRADOR', 0, 0, 0, 0, @userId, now(),
                    'Generado automáticamente para vigilancia por hito')
                RETURNING id
                """, new { projectId, userId }, tx);
        }

        await conn.ExecuteAsync(
            "DELETE FROM ss_presupuesto_vigilancia_hito WHERE presupuesto_id = @presupuestoId",
            new { presupuestoId }, tx);

        // Precio unitario vigente del servicio de vigilancia — mismo mecanismo de Ratios que los
        // demás materiales, snapshot al momento de guardar (igual que "S/ mes" en Dotación de personal).
        const string precioSql = """
            SELECT AVG(r.precio_unitario_promedio)
            FROM ss_ratio_proyecto r
            JOIN ss_material_familia f ON f.id = r.familia_id
            WHERE f.nombre = @familia AND r.incluido_manual_precio = true
            """;
        var precioUnitario = await conn.QuerySingleOrDefaultAsync<decimal?>(
            precioSql, new { familia = FamiliaVigilancia }, tx) ?? 0m;

        // Fechas del cronograma vigente — para recalcular Semanas cuando la fila trae un hito de salida.
        var fechasSql = $"""
            WITH {CronogramaVigenteCte}
            SELECT milestone_schedule_id AS HitoId, planned_start_date AS Fecha FROM cronograma_vigente
            """;
        var fechas = (await conn.QueryAsync<FechaHitoRow>(fechasSql, new { projectId }, tx))
            .ToDictionary(f => f.HitoId, f => f.Fecha);

        const decimal semanasPorMes = 4.345m;
        var filas = items.Select(i =>
        {
            var semanas = i.Semanas;
            if (i.HitoSalidaId.HasValue
                && fechas.TryGetValue(i.HitoId, out var fechaIngreso) && fechaIngreso.HasValue
                && fechas.TryGetValue(i.HitoSalidaId.Value, out var fechaSalida) && fechaSalida.HasValue)
            {
                var dias = fechaSalida.Value.DayNumber - fechaIngreso.Value.DayNumber;
                semanas = Math.Max(0, dias / 7m);
            }
            return new
            {
                presupuestoId,
                i.HitoId,
                i.HitoSalidaId,
                i.CantidadPuntos,
                Semanas = semanas,
                PrecioUnitario = precioUnitario,
                Total = i.CantidadPuntos * precioUnitario * (semanas / semanasPorMes),
            };
        });

        await conn.ExecuteAsync(
            """
            INSERT INTO ss_presupuesto_vigilancia_hito
                (presupuesto_id, hito_id, hito_salida_id, cantidad_puntos, semanas, precio_unitario, total)
            VALUES (@presupuestoId, @HitoId, @HitoSalidaId, @CantidadPuntos, @Semanas, @PrecioUnitario, @Total)
            """, filas, tx);

        await tx.CommitAsync();
    }
}
