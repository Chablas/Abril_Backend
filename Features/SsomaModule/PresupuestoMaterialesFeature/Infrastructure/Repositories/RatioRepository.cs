using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Dapper;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

public class RatioRepository : IRatioRepository
{
    private readonly IConfiguration _config;
    public RatioRepository(IConfiguration config) => _config = config;
    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]!);

    public async Task<List<RatioRawData>> ObtenerConsumosPorProyectoAsync(int projectId)
    {
        using var conn = Conn();
        const string sql = """
            SELECT
                f.id                                        AS FamiliaId,
                f.nombre                                    AS NombreFamilia,
                t.nombre                                    AS TipoMaterial,
                f.variable_base                             AS VariableBase,
                SUM(l.cantidad)                             AS CantidadTotal,
                CASE WHEN SUM(l.cantidad) > 0
                     THEN SUM(l.precio_total) / SUM(l.cantidad)
                     ELSE 0 END                             AS PrecioUnitarioPromedio,
                SUM(l.precio_total)                         AS PrecioTotal
            FROM ss_consumo_linea l
            JOIN ss_material_item i   ON i.id = l.item_id
            JOIN ss_material_familia f ON f.id = i.familia_id
            JOIN ss_material_tipo t   ON t.id = f.tipo_id
            WHERE l.project_id = @projectId
              AND l.estandarizado = true
              AND l.pertenece_ssoma = true
              AND (l.estado_revision IS NULL OR l.estado_revision = 'AUTORIZADO')
            GROUP BY f.id, f.nombre, t.nombre, f.variable_base
            ORDER BY SUM(l.precio_total) DESC
            """;
        var result = await conn.QueryAsync<RatioRawData>(sql, new { projectId });
        return result.ToList();
    }

    public async Task UpsertRatioAsync(int familiaId, int projectId, string variableBase,
        decimal cantidadTotal, decimal precioUnitarioPromedio, decimal valorDriver,
        decimal ratioCantidad, bool esOutlier)
    {
        using var conn = Conn();
        const string sql = """
            INSERT INTO ss_ratio_proyecto
              (familia_id, project_id, variable_base, cantidad_total, precio_unitario_promedio, valor_driver, ratio_cantidad, es_outlier)
            VALUES
              (@familiaId, @projectId, @variableBase, @cantidadTotal, @precioUnitarioPromedio, @valorDriver, @ratioCantidad, @esOutlier)
            ON CONFLICT (familia_id, project_id)
            DO UPDATE SET
              variable_base            = EXCLUDED.variable_base,
              cantidad_total           = EXCLUDED.cantidad_total,
              precio_unitario_promedio = EXCLUDED.precio_unitario_promedio,
              valor_driver             = EXCLUDED.valor_driver,
              ratio_cantidad           = EXCLUDED.ratio_cantidad,
              es_outlier               = EXCLUDED.es_outlier
            """;
        await conn.ExecuteAsync(sql, new { familiaId, projectId, variableBase, cantidadTotal,
            precioUnitarioPromedio = (double)precioUnitarioPromedio,
            valorDriver = (double)valorDriver, ratioCantidad = (double)ratioCantidad, esOutlier });
    }

    public async Task<List<RatioProyectoDto>> ObtenerRatiosPorProyectoAsync(int projectId)
    {
        using var conn = Conn();
        const string sql = """
            SELECT r.id, r.familia_id AS FamiliaId, f.nombre AS NombreFamilia, t.nombre AS TipoMaterial,
                   r.project_id AS ProjectId, p.project_description AS ProjectDescription,
                   r.variable_base AS VariableBase, r.cantidad_total AS CantidadTotal,
                   r.precio_unitario_promedio AS PrecioUnitarioPromedio,
                   r.valor_driver AS ValorDriver, r.ratio_cantidad AS RatioCantidad,
                   r.es_outlier AS EsOutlier
            FROM ss_ratio_proyecto r
            JOIN ss_material_familia f ON f.id = r.familia_id
            JOIN ss_material_tipo t    ON t.id = f.tipo_id
            JOIN project p             ON p.project_id = r.project_id
            WHERE r.project_id = @projectId
            ORDER BY r.ratio_cantidad DESC
            """;
        var result = await conn.QueryAsync<RatioProyectoDto>(sql, new { projectId });
        return result.ToList();
    }

    public async Task<List<RatioProyectoDto>> ObtenerRatiosPorFamiliaAsync(int familiaId)
    {
        using var conn = Conn();
        const string sql = """
            SELECT r.id, r.familia_id AS FamiliaId, f.nombre AS NombreFamilia, t.nombre AS TipoMaterial,
                   r.project_id AS ProjectId, p.project_description AS ProjectDescription,
                   r.variable_base AS VariableBase, r.cantidad_total AS CantidadTotal,
                   r.precio_unitario_promedio AS PrecioUnitarioPromedio,
                   r.valor_driver AS ValorDriver, r.ratio_cantidad AS RatioCantidad,
                   r.es_outlier AS EsOutlier
            FROM ss_ratio_proyecto r
            JOIN ss_material_familia f ON f.id = r.familia_id
            JOIN ss_material_tipo t    ON t.id = f.tipo_id
            JOIN project p             ON p.project_id = r.project_id
            WHERE r.familia_id = @familiaId
            ORDER BY r.ratio_cantidad
            """;
        var result = await conn.QueryAsync<RatioProyectoDto>(sql, new { familiaId });
        return result.ToList();
    }

    public async Task<List<ResumenProyectoRatioDto>> ObtenerResumenAsync()
    {
        using var conn = Conn();
        const string sql = """
            SELECT r.project_id AS ProjectId, p.project_description AS ProjectDescription,
                   COUNT(DISTINCT r.familia_id) AS FamiliasCalculadas,
                   SUM(r.cantidad_total * r.precio_unitario_promedio) AS TotalGastoSsoma,
                   c.fecha_min AS FechaMin, c.fecha_max AS FechaMax
            FROM ss_ratio_proyecto r
            JOIN project p ON p.project_id = r.project_id
            LEFT JOIN (
                SELECT project_id, MIN(fecha_min) AS fecha_min, MAX(fecha_max) AS fecha_max
                FROM ss_consumo_carga WHERE estado = 'ACTIVA' GROUP BY project_id
            ) c ON c.project_id = r.project_id
            GROUP BY r.project_id, p.project_description, c.fecha_min, c.fecha_max
            ORDER BY TotalGastoSsoma DESC
            """;
        var result = await conn.QueryAsync<ResumenProyectoRatioDto>(sql);
        return result.ToList();
    }
}
