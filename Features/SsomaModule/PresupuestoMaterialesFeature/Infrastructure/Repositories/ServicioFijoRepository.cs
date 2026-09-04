using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Dapper;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

/// <summary>Servicios de costo fijo (VariableBase = FIJO en Catálogo) planificados manualmente por
/// proyecto — a diferencia de los materiales por ratio, su cantidad no escala con HH/Área/Trabajadores,
/// así que el responsable la tipea a mano. El precio unitario sí sigue viniendo de Ratios (mismo
/// mecanismo que Vigilancia), snapshot al momento de guardar.</summary>
public class ServicioFijoRepository : IServicioFijoRepository
{
    private readonly IConfiguration _config;
    public ServicioFijoRepository(IConfiguration config) => _config = config;
    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]!);

    public async Task<List<FamiliaFijaDisponibleDto>> ObtenerFamiliasFijasDisponiblesAsync()
    {
        using var conn = Conn();
        const string sql = """
            SELECT f.id AS FamiliaId, f.nombre AS NombreFamilia, f.unidad_medida AS UnidadMedida
            FROM ss_material_familia f
            WHERE f.variable_base = 'FIJO' AND f.pertenece_ssoma = true AND f.activo = true
            ORDER BY f.nombre
            """;
        var result = await conn.QueryAsync<FamiliaFijaDisponibleDto>(sql);
        return result.ToList();
    }

    public async Task<List<ServicioFijoDto>> ObtenerPorProyectoAsync(int projectId)
    {
        using var conn = Conn();
        const string sql = """
            SELECT f.id AS FamiliaId, f.nombre AS NombreFamilia, f.unidad_medida AS UnidadMedida,
                   im.metrado AS Metrado, im.precio_unitario AS PrecioUnitario, im.total AS Total,
                   im.descripcion AS Descripcion
            FROM ss_presupuesto_item_metrado im
            JOIN ss_presupuesto p       ON p.id = im.presupuesto_id
            JOIN ss_material_familia f  ON f.id = im.familia_id
            WHERE p.project_id = @projectId
            ORDER BY f.nombre
            """;
        var result = await conn.QueryAsync<ServicioFijoDto>(sql, new { projectId });
        return result.ToList();
    }

    public async Task GuardarAsync(int projectId, List<ServicioFijoItemInputDto> items, int userId)
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
                    'Generado automáticamente para servicios de costo fijo')
                RETURNING id
                """, new { projectId, userId }, tx);
        }

        await conn.ExecuteAsync(
            "DELETE FROM ss_presupuesto_item_metrado WHERE presupuesto_id = @presupuestoId",
            new { presupuestoId }, tx);

        if (items.Count == 0)
        {
            await PresupuestoTotalHelper.RecalcularTotalAsync(conn, presupuestoId.Value, tx);
            await tx.CommitAsync();
            return;
        }

        // Precio unitario vigente de cada família — mismo mecanismo de Ratios que el resto de
        // materiales, snapshot al momento de guardar (igual que "S/ mes" en Dotación de personal
        // y el precio de Vigilancia).
        var familiaIds = items.Select(i => i.FamiliaId).Distinct().ToArray();
        const string preciosSql = """
            SELECT r.familia_id AS FamiliaId, AVG(r.precio_unitario_promedio) AS Precio
            FROM ss_ratio_proyecto r
            WHERE r.familia_id = ANY(@familiaIds) AND r.incluido_manual_precio = true
            GROUP BY r.familia_id
            """;
        var precios = (await conn.QueryAsync<(int FamiliaId, decimal Precio)>(
                preciosSql, new { familiaIds }, tx))
            .ToDictionary(p => p.FamiliaId, p => p.Precio);

        var filas = items.Select(i =>
        {
            var precio = precios.GetValueOrDefault(i.FamiliaId, 0m);
            return new
            {
                presupuestoId,
                i.FamiliaId,
                i.Metrado,
                PrecioUnitario = precio,
                Total = i.Metrado * precio,
                i.Descripcion,
            };
        });

        await conn.ExecuteAsync(
            """
            INSERT INTO ss_presupuesto_item_metrado
                (presupuesto_id, familia_id, metrado, precio_unitario, total, descripcion)
            VALUES (@presupuestoId, @FamiliaId, @Metrado, @PrecioUnitario, @Total, @Descripcion)
            """, filas, tx);

        await PresupuestoTotalHelper.RecalcularTotalAsync(conn, presupuestoId.Value, tx);

        await tx.CommitAsync();
    }
}
