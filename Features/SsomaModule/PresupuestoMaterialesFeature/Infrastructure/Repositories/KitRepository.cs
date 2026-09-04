using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Dapper;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

public class KitRepository : IKitRepository
{
    private readonly IConfiguration _config;
    public KitRepository(IConfiguration config) => _config = config;
    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]!);

    public async Task<List<KitResumenDto>> ListarAsync(int? tipoId)
    {
        using var conn = Conn();
        const string sql = """
            SELECT k.id AS Id, k.nombre AS Nombre, k.tipo_id AS TipoId, t.nombre AS NombreTipo
            FROM ss_kit k
            JOIN ss_material_tipo t ON t.id = k.tipo_id
            WHERE k.activo = true AND (@tipoId IS NULL OR k.tipo_id = @tipoId)
            ORDER BY k.nombre
            """;
        var result = await conn.QueryAsync<KitResumenDto>(sql, new { tipoId });
        return result.ToList();
    }

    public async Task<KitDetalleDto?> ObtenerAsync(int kitId)
    {
        using var conn = Conn();
        var kit = await conn.QuerySingleOrDefaultAsync<KitResumenDto>(
            """
            SELECT k.id AS Id, k.nombre AS Nombre, k.tipo_id AS TipoId, t.nombre AS NombreTipo
            FROM ss_kit k JOIN ss_material_tipo t ON t.id = k.tipo_id
            WHERE k.id = @kitId
            """, new { kitId });
        if (kit == null) return null;

        var items = await conn.QueryAsync<KitItemDto>(
            """
            SELECT ki.id AS Id, ki.familia_id AS FamiliaId, f.nombre AS NombreFamilia,
                   ki.cantidad_por_kit AS CantidadPorKit, ki.es_consumible AS EsConsumible
            FROM ss_kit_item ki
            JOIN ss_material_familia f ON f.id = ki.familia_id
            WHERE ki.kit_id = @kitId
            ORDER BY f.nombre
            """, new { kitId });

        return new KitDetalleDto
        {
            Id = kit.Id,
            Nombre = kit.Nombre,
            TipoId = kit.TipoId,
            NombreTipo = kit.NombreTipo,
            Items = items.ToList(),
        };
    }

    public async Task<int> CrearAsync(KitCreateDto dto)
    {
        using var conn = Conn();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        var kitId = await conn.QuerySingleAsync<int>(
            """
            INSERT INTO ss_kit (nombre, tipo_id, activo, creado_en)
            VALUES (@Nombre, @TipoId, true, now())
            RETURNING id
            """, new { dto.Nombre, dto.TipoId }, tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO ss_kit_item (kit_id, familia_id, cantidad_por_kit, es_consumible)
            VALUES (@kitId, @FamiliaId, @CantidadPorKit, @EsConsumible)
            """,
            dto.Items.Select(i => new { kitId, i.FamiliaId, i.CantidadPorKit, i.EsConsumible }),
            tx);

        await tx.CommitAsync();
        return kitId;
    }

    /// <summary>Vista previa (antes de guardar): mismo precio unitario de Ratios que se va a snapshotear
    /// si el usuario guarda (GuardarEnProyectoAsync) — así el total que se ve acá coincide con el que
    /// va a quedar guardado, en vez de mostrar siempre S/ 0,00 hasta guardar.</summary>
    public async Task<List<KitCalculoLineaDto>> CalcularAsync(int kitId, decimal cantidadKits)
    {
        using var conn = Conn();
        const string sql = """
            SELECT ki.familia_id AS FamiliaId, f.nombre AS NombreFamilia,
                   ki.cantidad_por_kit AS CantidadPorKit,
                   ki.cantidad_por_kit * @cantidadKits AS CantidadTotal,
                   ki.es_consumible AS EsConsumible,
                   COALESCE(precios.precio, 0) AS PrecioUnitario,
                   ki.cantidad_por_kit * @cantidadKits * COALESCE(precios.precio, 0) AS Total
            FROM ss_kit_item ki
            JOIN ss_material_familia f ON f.id = ki.familia_id
            LEFT JOIN (
                SELECT r.familia_id, AVG(r.precio_unitario_promedio) AS precio
                FROM ss_ratio_proyecto r
                WHERE r.incluido_manual_precio = true
                GROUP BY r.familia_id
            ) precios ON precios.familia_id = ki.familia_id
            WHERE ki.kit_id = @kitId
            ORDER BY f.nombre
            """;
        var result = await conn.QueryAsync<KitCalculoLineaDto>(sql, new { kitId, cantidadKits });
        return result.ToList();
    }

    private class KitGuardadoLineaRow
    {
        public int KitId { get; set; }
        public string NombreKit { get; set; } = "";
        public decimal CantidadKits { get; set; }
        public int FamiliaId { get; set; }
        public string NombreFamilia { get; set; } = "";
        public decimal CantidadPorKit { get; set; }
        public decimal CantidadTotal { get; set; }
        public bool EsConsumible { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Total { get; set; }
    }

    /// <summary>Devuelve TODOS los kits guardados en el presupuesto del proyecto (puede haber varios
    /// tipos a la vez — ej. Botiquín x3 Y Estación de Emergencia x1 — no solo el último guardado).</summary>
    public async Task<List<KitProyectoGuardadoDto>> ObtenerGuardadosPorProyectoAsync(int projectId)
    {
        using var conn = Conn();
        const string sql = """
            SELECT ki.kit_id AS KitId, k.nombre AS NombreKit, ki.cantidad_kits AS CantidadKits,
                   ki.familia_id AS FamiliaId, f.nombre AS NombreFamilia,
                   ki.cantidad_por_kit AS CantidadPorKit, ki.cantidad_total AS CantidadTotal,
                   ki.es_consumible AS EsConsumible, ki.precio_unitario AS PrecioUnitario, ki.total AS Total
            FROM ss_presupuesto_kit_item ki
            JOIN ss_presupuesto p      ON p.id = ki.presupuesto_id
            JOIN ss_kit k              ON k.id = ki.kit_id
            JOIN ss_material_familia f ON f.id = ki.familia_id
            WHERE p.project_id = @projectId
            ORDER BY k.nombre, f.nombre
            """;
        var filas = (await conn.QueryAsync<KitGuardadoLineaRow>(sql, new { projectId })).ToList();

        return filas
            .GroupBy(f => new { f.KitId, f.NombreKit, f.CantidadKits })
            .Select(g => new KitProyectoGuardadoDto
            {
                KitId = g.Key.KitId,
                NombreKit = g.Key.NombreKit,
                CantidadKits = g.Key.CantidadKits,
                Lineas = g.Select(f => new KitCalculoLineaDto
                {
                    FamiliaId = f.FamiliaId,
                    NombreFamilia = f.NombreFamilia,
                    CantidadPorKit = f.CantidadPorKit,
                    CantidadTotal = f.CantidadTotal,
                    EsConsumible = f.EsConsumible,
                    PrecioUnitario = f.PrecioUnitario,
                    Total = f.Total,
                }).ToList(),
                Total = g.Sum(f => f.Total),
            })
            .ToList();
    }

    private static async Task<int> ObtenerOCrearPresupuestoAsync(NpgsqlConnection conn, NpgsqlTransaction tx, int projectId, int userId)
    {
        var presupuestoId = await conn.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT id FROM ss_presupuesto WHERE project_id = @projectId
            ORDER BY generado_en DESC LIMIT 1
            """, new { projectId }, tx);

        if (presupuestoId != null) return presupuestoId.Value;

        return await conn.QuerySingleAsync<int>(
            """
            INSERT INTO ss_presupuesto (project_id, version, estado, hh_usado, area_usada,
                trabajadores_usados, total_estimado, generado_por, generado_en, notas)
            VALUES (@projectId, 1, 'BORRADOR', 0, 0, 0, 0, @userId, now(),
                'Generado automáticamente para kits/BOM')
            RETURNING id
            """, new { projectId, userId }, tx);
    }

    /// <summary>Quita un kit guardado del presupuesto (ej. si se cargó de más o ya no corresponde) sin
    /// tocar los demás kits guardados del proyecto.</summary>
    public async Task EliminarDelProyectoAsync(int projectId, int kitId)
    {
        using var conn = Conn();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        var presupuestoId = await conn.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT id FROM ss_presupuesto WHERE project_id = @projectId
            ORDER BY generado_en DESC LIMIT 1
            """, new { projectId }, tx);
        if (presupuestoId == null) { await tx.CommitAsync(); return; }

        await conn.ExecuteAsync(
            "DELETE FROM ss_presupuesto_kit_item WHERE presupuesto_id = @presupuestoId AND kit_id = @kitId",
            new { presupuestoId, kitId }, tx);

        await PresupuestoTotalHelper.RecalcularTotalAsync(conn, presupuestoId.Value, tx);
        await tx.CommitAsync();
    }

    /// <summary>Guarda (reemplaza) UN kit del proyecto por su kitId — no toca los demás kits ya
    /// guardados con otro kitId, así que un proyecto puede tener varios kits a la vez (ej. Botiquín
    /// x3 Y Estación de Emergencia x1 simultáneamente).</summary>
    public async Task GuardarEnProyectoAsync(int projectId, int kitId, decimal cantidadKits, int userId)
    {
        using var conn = Conn();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        var presupuestoId = await ObtenerOCrearPresupuestoAsync(conn, tx, projectId, userId);

        await conn.ExecuteAsync(
            "DELETE FROM ss_presupuesto_kit_item WHERE presupuesto_id = @presupuestoId AND kit_id = @kitId",
            new { presupuestoId, kitId }, tx);

        var bom = (await conn.QueryAsync<(int FamiliaId, decimal CantidadPorKit, bool EsConsumible)>(
            """
            SELECT familia_id AS FamiliaId, cantidad_por_kit AS CantidadPorKit, es_consumible AS EsConsumible
            FROM ss_kit_item WHERE kit_id = @kitId
            """, new { kitId }, tx)).ToList();

        if (bom.Count > 0)
        {
            // Precio unitario vigente de cada família — mismo mecanismo de Ratios que Servicios y
            // Vigilancia, snapshot al momento de guardar.
            var familiaIds = bom.Select(b => b.FamiliaId).Distinct().ToArray();
            const string preciosSql = """
                SELECT r.familia_id AS FamiliaId, AVG(r.precio_unitario_promedio) AS Precio
                FROM ss_ratio_proyecto r
                WHERE r.familia_id = ANY(@familiaIds) AND r.incluido_manual_precio = true
                GROUP BY r.familia_id
                """;
            var precios = (await conn.QueryAsync<(int FamiliaId, decimal Precio)>(
                    preciosSql, new { familiaIds }, tx))
                .ToDictionary(p => p.FamiliaId, p => p.Precio);

            var filas = bom.Select(b =>
            {
                var precio = precios.GetValueOrDefault(b.FamiliaId, 0m);
                var cantidadTotal = b.CantidadPorKit * cantidadKits;
                return new
                {
                    presupuestoId,
                    kitId,
                    cantidadKits,
                    b.FamiliaId,
                    b.CantidadPorKit,
                    CantidadTotal = cantidadTotal,
                    PrecioUnitario = precio,
                    Total = cantidadTotal * precio,
                    b.EsConsumible,
                };
            });

            await conn.ExecuteAsync(
                """
                INSERT INTO ss_presupuesto_kit_item
                    (presupuesto_id, kit_id, cantidad_kits, familia_id, cantidad_por_kit, cantidad_total, precio_unitario, total, es_consumible)
                VALUES (@presupuestoId, @kitId, @cantidadKits, @FamiliaId, @CantidadPorKit, @CantidadTotal, @PrecioUnitario, @Total, @EsConsumible)
                """, filas, tx);
        }

        await PresupuestoTotalHelper.RecalcularTotalAsync(conn, presupuestoId, tx);

        await tx.CommitAsync();
    }
}
