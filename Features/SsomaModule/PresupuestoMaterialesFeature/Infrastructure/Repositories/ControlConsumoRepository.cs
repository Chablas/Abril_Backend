using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Dapper;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

public class ControlConsumoRepository : IControlConsumoRepository
{
    private readonly IConfiguration _config;
    public ControlConsumoRepository(IConfiguration config) => _config = config;
    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]!);

    public async Task<int> SiguienteSemanaNumAsync(int presupuestoId)
    {
        using var conn = Conn();
        var max = await conn.ExecuteScalarAsync<int?>(
            "SELECT MAX(semana_num) FROM ss_control_semana WHERE presupuesto_id = @presupuestoId",
            new { presupuestoId });
        return (max ?? 0) + 1;
    }

    public async Task<int> CrearSemanaAsync(int presupuestoId, int projectId, int semanaNum,
        DateOnly fechaInicio, DateOnly fechaFin, string? obs, int? userId)
    {
        using var conn = Conn();
        return await conn.ExecuteScalarAsync<int>("""
            INSERT INTO ss_control_semana
              (presupuesto_id, project_id, semana_num, fecha_inicio, fecha_fin, observaciones, registrado_por)
            VALUES
              (@presupuestoId, @projectId, @semanaNum, @fechaInicio, @fechaFin, @obs, @userId)
            RETURNING id
            """,
            new { presupuestoId, projectId, semanaNum,
                  fechaInicio = fechaInicio.ToDateTime(TimeOnly.MinValue),
                  fechaFin    = fechaFin.ToDateTime(TimeOnly.MinValue),
                  obs, userId });
    }

    public async Task UpsertLineasAsync(int controlId, IEnumerable<RegistrarConsumoLineaDto> lineas)
    {
        using var conn = Conn();
        // Eliminar las líneas existentes del control y reinsertarlas (simplicity > partial upsert)
        await conn.ExecuteAsync(
            "DELETE FROM ss_control_semana_linea WHERE control_id = @controlId",
            new { controlId });

        var rows = lineas.Where(l => l.CantidadReal > 0).Select(l => new
        {
            controlId,
            l.FamiliaId,
            l.CantidadReal,
            l.PrecioUnitario,
            l.Notas
        });

        if (!rows.Any()) return;

        await conn.ExecuteAsync("""
            INSERT INTO ss_control_semana_linea
              (control_id, familia_id, cantidad_real, precio_unitario, notas)
            VALUES
              (@controlId, @FamiliaId, @CantidadReal, @PrecioUnitario, @Notas)
            """, rows);
    }

    public async Task CerrarSemanaAsync(int controlId)
    {
        using var conn = Conn();
        await conn.ExecuteAsync("""
            UPDATE ss_control_semana
            SET estado = 'CERRADO', cerrado_en = NOW()
            WHERE id = @controlId AND estado = 'ABIERTO'
            """, new { controlId });
    }

    public async Task<ControlSemanaDto?> ObtenerSemanaAsync(int controlId)
    {
        using var conn = Conn();
        var semana = await conn.QueryFirstOrDefaultAsync<ControlSemanaDto>("""
            SELECT cs.id, cs.presupuesto_id AS PresupuestoId, cs.project_id AS ProjectId,
                   p.project_description AS ProjectDescription,
                   cs.semana_num AS SemanaNum, cs.fecha_inicio AS FechaInicio,
                   cs.fecha_fin AS FechaFin, cs.estado AS Estado,
                   cs.observaciones AS Observaciones, cs.registrado_en AS RegistradoEn
            FROM ss_control_semana cs
            JOIN project p ON p.project_id = cs.project_id
            WHERE cs.id = @controlId
            """, new { controlId });

        if (semana is null) return null;

        semana.Lineas = (await conn.QueryAsync<ControlSemanaLineaDto>("""
            SELECT l.id, l.familia_id AS FamiliaId, f.nombre AS NombreFamilia,
                   t.nombre AS NombreTipo, l.cantidad_real AS CantidadReal,
                   l.precio_unitario AS PrecioUnitario, l.total_real AS TotalReal, l.notas AS Notas
            FROM ss_control_semana_linea l
            JOIN ss_material_familia f ON f.id = l.familia_id
            JOIN ss_material_tipo t    ON t.id = f.tipo_id
            WHERE l.control_id = @controlId
            ORDER BY t.nombre, f.nombre
            """, new { controlId })).ToList();

        return semana;
    }

    public async Task<List<ControlSemanaDto>> ListarSemanasPorPresupuestoAsync(int presupuestoId)
    {
        using var conn = Conn();
        var semanas = (await conn.QueryAsync<ControlSemanaDto>("""
            SELECT cs.id, cs.presupuesto_id AS PresupuestoId, cs.project_id AS ProjectId,
                   p.project_description AS ProjectDescription,
                   cs.semana_num AS SemanaNum, cs.fecha_inicio AS FechaInicio,
                   cs.fecha_fin AS FechaFin, cs.estado AS Estado,
                   cs.observaciones AS Observaciones, cs.registrado_en AS RegistradoEn
            FROM ss_control_semana cs
            JOIN project p ON p.project_id = cs.project_id
            WHERE cs.presupuesto_id = @presupuestoId
            ORDER BY cs.semana_num DESC
            """, new { presupuestoId })).ToList();
        return semanas;
    }

    public async Task<DashboardPresupuestoDto?> ObtenerDashboardAsync(int presupuestoId)
    {
        using var conn = Conn();

        // Header del presupuesto
        var header = await conn.QueryFirstOrDefaultAsync<DashboardPresupuestoDto>("""
            SELECT p.id AS PresupuestoId, p.project_id AS ProjectId,
                   pr.project_description AS ProjectDescription, p.version,
                   p.total_estimado AS TotalPresupuestado,
                   (SELECT COUNT(*) FROM ss_control_semana WHERE presupuesto_id = p.id) AS SemanasRegistradas
            FROM ss_presupuesto p
            JOIN project pr ON pr.project_id = p.project_id
            WHERE p.id = @presupuestoId
            """, new { presupuestoId });

        if (header is null) return null;

        // Líneas con consumo acumulado — el consumo real sale del Kardex (ss_consumo_linea), no de
        // un registro manual: así el dashboard nunca queda desactualizado ni exige doble digitación
        // de lo que ya entra por la subida semanal del S10. Segunda rama del UNION: familias con
        // consumo real pero SIN línea presupuestada — se pidieron por fuera de lo planificado y se
        // resaltan aparte en vez de perderse silenciosamente del comparativo.
        var lineas = (await conn.QueryAsync<DashboardLineaDto>("""
            WITH consumo AS (
              SELECT i.familia_id, l.project_id,
                     SUM(l.cantidad)     AS cantidad_real,
                     SUM(l.precio_total) AS total_real
              FROM ss_consumo_linea l
              JOIN ss_material_item i ON i.id = l.item_id
              WHERE l.activo = true AND l.pertenece_ssoma = true
              GROUP BY i.familia_id, l.project_id
            ),
            combinado AS (
            SELECT
              pl.familia_id                                                    AS FamiliaId,
              f.nombre                                                         AS NombreFamilia,
              t.id                                                             AS TipoId,
              pl.variable_base                                                 AS VariableBase,
              COALESCE(pl.cantidad_manual, pl.cantidad_estimada)               AS CantidadPresupuestada,
              COALESCE(kx.cantidad_real, 0)                                    AS CantidadConsumida,
              COALESCE(pl.precio_manual, pl.precio_unitario)                   AS PrecioUnitario,
              COALESCE(pl.cantidad_manual, pl.cantidad_estimada)
                * COALESCE(pl.precio_manual, pl.precio_unitario)               AS TotalPresupuestado,
              COALESCE(kx.total_real, 0)                                       AS TotalConsumido,
              CASE
                WHEN COALESCE(pl.cantidad_manual, pl.cantidad_estimada) = 0        THEN 'SIN_PRESUPUESTO'
                WHEN COALESCE(kx.cantidad_real, 0)
                       >= COALESCE(pl.cantidad_manual, pl.cantidad_estimada)       THEN 'ALERTA'
                WHEN COALESCE(kx.cantidad_real, 0)
                       >= COALESCE(pl.cantidad_manual, pl.cantidad_estimada) * 0.8 THEN 'ADVERTENCIA'
                ELSE 'OK'
              END                                                              AS Semaforo,
              false                                                            AS FueraDePresupuesto
            FROM ss_presupuesto_detalle pl
            JOIN ss_material_familia f  ON f.id = pl.familia_id
            JOIN ss_material_tipo t     ON t.id = pl.tipo_id
            JOIN ss_presupuesto pr      ON pr.id = pl.presupuesto_id
            LEFT JOIN consumo kx ON kx.familia_id = pl.familia_id AND kx.project_id = pr.project_id
            WHERE pl.presupuesto_id = @presupuestoId

            UNION ALL

            SELECT
              kx.familia_id                                                    AS FamiliaId,
              f.nombre                                                         AS NombreFamilia,
              t.id                                                             AS TipoId,
              f.variable_base                                                  AS VariableBase,
              0::numeric                                                       AS CantidadPresupuestada,
              kx.cantidad_real                                                 AS CantidadConsumida,
              0::numeric                                                       AS PrecioUnitario,
              0::numeric                                                       AS TotalPresupuestado,
              kx.total_real                                                    AS TotalConsumido,
              'FUERA_DE_PRESUPUESTO'                                           AS Semaforo,
              true                                                             AS FueraDePresupuesto
            FROM consumo kx
            JOIN ss_material_familia f ON f.id = kx.familia_id
            JOIN ss_material_tipo t    ON t.id = f.tipo_id
            WHERE kx.project_id = @projectId
              AND NOT EXISTS (
                SELECT 1 FROM ss_presupuesto_detalle pl2
                WHERE pl2.presupuesto_id = @presupuestoId AND pl2.familia_id = kx.familia_id
              )
            )
            SELECT * FROM combinado
            ORDER BY
              "FueraDePresupuesto" DESC,
              CASE WHEN "Semaforo" = 'ALERTA' THEN 1 WHEN "Semaforo" = 'ADVERTENCIA' THEN 2 ELSE 3 END,
              "NombreFamilia"
            """, new { presupuestoId, projectId = header.ProjectId })).ToList();

        header.TotalConsumido        = lineas.Sum(l => l.TotalConsumido);
        header.FamiliasEnAlerta      = lineas.Count(l => l.Semaforo == "ALERTA");
        header.FamiliasEnAdvertencia = lineas.Count(l => l.Semaforo == "ADVERTENCIA");
        header.FamiliasFueraDePresupuesto = lineas.Count(l => l.FueraDePresupuesto);

        // Necesitamos NombreTipo para agrupar — segunda query ligera
        var tipos = (await conn.QueryAsync<(int Id, string Nombre)>(
            "SELECT id, nombre FROM ss_material_tipo ORDER BY nombre")).ToList();
        var tipoMap = tipos.ToDictionary(t => t.Id, t => t.Nombre);

        header.Tipos = lineas
            .GroupBy(l => l.TipoId)
            .Select(g => new DashboardTipoDto
            {
                TipoId             = g.Key,
                NombreTipo         = tipoMap.GetValueOrDefault(g.Key, g.Key.ToString()),
                TotalPresupuestado = g.Sum(l => l.TotalPresupuestado),
                TotalConsumido     = g.Sum(l => l.TotalConsumido),
                Familias           = g.ToList()
            })
            .OrderByDescending(t => t.PctConsumido)
            .ToList();

        return header;
    }

    /// <summary>Vista gerencial acumulada: un renglón por proyecto (usando su presupuesto más
    /// reciente) con total presupuestado vs. consumido real del Kardex — para el dashboard "SSOMA"
    /// que resume todos los proyectos de un vistazo, en vez de tener que entrar uno por uno.</summary>
    public async Task<DashboardAcumuladoDto> ObtenerDashboardAcumuladoAsync()
    {
        using var conn = Conn();
        var proyectos = (await conn.QueryAsync<DashboardAcumuladoProyectoDto>(
            """
            WITH ultimo_presupuesto AS (
              SELECT DISTINCT ON (project_id) id, project_id, version, total_estimado
              FROM ss_presupuesto
              ORDER BY project_id, version DESC
            ),
            consumo AS (
              SELECT i.familia_id, l.project_id, SUM(l.precio_total) AS total_real
              FROM ss_consumo_linea l
              JOIN ss_material_item i ON i.id = l.item_id
              WHERE l.activo = true AND l.pertenece_ssoma = true
              GROUP BY i.familia_id, l.project_id
            ),
            consumo_proyecto AS (
              SELECT project_id, SUM(total_real) AS total_consumido
              FROM consumo GROUP BY project_id
            ),
            fuera_de_presupuesto AS (
              SELECT up.project_id, COUNT(*) AS n
              FROM ultimo_presupuesto up
              JOIN consumo c ON c.project_id = up.project_id
              WHERE NOT EXISTS (
                SELECT 1 FROM ss_presupuesto_detalle pd
                WHERE pd.presupuesto_id = up.id AND pd.familia_id = c.familia_id
              )
              GROUP BY up.project_id
            )
            SELECT
              up.id AS PresupuestoId, up.project_id AS ProjectId,
              pr.project_description AS ProjectDescription, up.version AS Version,
              up.total_estimado AS TotalPresupuestado,
              COALESCE(cp.total_consumido, 0) AS TotalConsumido,
              COALESCE(fp.n, 0) AS FamiliasFueraDePresupuesto,
              CASE
                WHEN up.total_estimado = 0 THEN 'SIN_PRESUPUESTO'
                WHEN COALESCE(cp.total_consumido, 0) >= up.total_estimado       THEN 'ALERTA'
                WHEN COALESCE(cp.total_consumido, 0) >= up.total_estimado * 0.8 THEN 'ADVERTENCIA'
                ELSE 'OK'
              END AS Semaforo
            FROM ultimo_presupuesto up
            JOIN project pr ON pr.project_id = up.project_id
            LEFT JOIN consumo_proyecto cp ON cp.project_id = up.project_id
            LEFT JOIN fuera_de_presupuesto fp ON fp.project_id = up.project_id
            ORDER BY
              CASE
                WHEN COALESCE(fp.n, 0) > 0 THEN 0
                WHEN COALESCE(cp.total_consumido, 0) >= up.total_estimado       THEN 1
                WHEN COALESCE(cp.total_consumido, 0) >= up.total_estimado * 0.8 THEN 2
                ELSE 3
              END,
              pr.project_description
            """)).ToList();

        return new DashboardAcumuladoDto
        {
            TotalProyectos     = proyectos.Count,
            TotalPresupuestado = proyectos.Sum(p => p.TotalPresupuestado),
            TotalConsumido     = proyectos.Sum(p => p.TotalConsumido),
            ProyectosEnAlerta      = proyectos.Count(p => p.Semaforo == "ALERTA"),
            ProyectosEnAdvertencia = proyectos.Count(p => p.Semaforo == "ADVERTENCIA"),
            ProyectosConFueraDePresupuesto = proyectos.Count(p => p.FamiliasFueraDePresupuesto > 0),
            Proyectos = proyectos,
        };
    }

    /// <summary>Presupuesto más reciente (mayor versión) de un proyecto — para pantallas que solo
    /// conocen el projectId y quieren el dashboard sin exigirle al usuario elegir versión.</summary>
    public async Task<int?> ObtenerPresupuestoVigenteIdAsync(int projectId)
    {
        using var conn = Conn();
        return await conn.QuerySingleOrDefaultAsync<int?>(
            "SELECT id FROM ss_presupuesto WHERE project_id = @projectId ORDER BY version DESC LIMIT 1",
            new { projectId });
    }
}
