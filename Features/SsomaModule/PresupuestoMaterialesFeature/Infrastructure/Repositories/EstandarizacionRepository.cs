using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

public class EstandarizacionRepository : IEstandarizacionRepository
{
    private readonly IDbContextFactory<AppDbContext> _factory;
    private readonly IConfiguration _config;

    public EstandarizacionRepository(IDbContextFactory<AppDbContext> factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]!);

    public async Task<MatchResult?> BuscarPorAliasExactoAsync(string textoCrudoNorm)
    {
        using var conn = Conn();
        const string sql = """
            SELECT i.id AS ItemId, i.nombre AS NombreItem,
                   f.id AS FamiliaId, f.nombre AS NombreFamilia,
                   f.pertenece_ssoma AS PerteneceSsoma,
                   1.0 AS Score, 'ALIAS' AS Metodo,
                   a.factor_conversion AS FactorConversion
            FROM ss_material_alias a
            JOIN ss_material_item i ON i.id = a.item_id
            JOIN ss_material_familia f ON f.id = i.familia_id
            WHERE a.texto_crudo_norm = @norm AND i.no_usar = false AND i.activo = true
            LIMIT 1
            """;
        return await conn.QueryFirstOrDefaultAsync<MatchResult>(sql, new { norm = textoCrudoNorm });
    }

    public async Task<MatchResult?> BuscarPorNombreExactoAsync(string nombreNorm)
    {
        using var conn = Conn();
        const string sql = """
            SELECT i.id AS ItemId, i.nombre AS NombreItem,
                   f.id AS FamiliaId, f.nombre AS NombreFamilia,
                   f.pertenece_ssoma AS PerteneceSsoma,
                   1.0 AS Score, 'EXACTO' AS Metodo
            FROM ss_material_item i
            JOIN ss_material_familia f ON f.id = i.familia_id
            WHERE i.nombre_normalizado = @norm AND i.no_usar = false AND i.activo = true
            LIMIT 1
            """;
        return await conn.QueryFirstOrDefaultAsync<MatchResult>(sql, new { norm = nombreNorm });
    }

    public async Task<List<MatchResult>> BuscarPorTrigramAsync(string textoCrudoNorm, decimal umbralMinimo, int topN = 5)
    {
        using var conn = Conn();
        const string sql = """
            SELECT i.id AS ItemId, i.nombre AS NombreItem,
                   f.id AS FamiliaId, f.nombre AS NombreFamilia,
                   f.pertenece_ssoma AS PerteneceSsoma,
                   similarity(i.nombre_normalizado, @texto) AS Score,
                   'FUZZY' AS Metodo
            FROM ss_material_item i
            JOIN ss_material_familia f ON f.id = i.familia_id
            WHERE i.no_usar = false AND i.activo = true
              AND similarity(i.nombre_normalizado, @texto) >= @umbral
            ORDER BY similarity(i.nombre_normalizado, @texto) DESC
            LIMIT @topN
            """;
        var resultados = await conn.QueryAsync<MatchResult>(sql,
            new { texto = textoCrudoNorm, umbral = (double)umbralMinimo, topN });
        return resultados.ToList();
    }

    public async Task CrearAliasAsync(string textoCrudo, string textoCrudoNorm, int itemId, string origen, decimal confianza)
    {
        using var conn = Conn();
        const string sql = """
            INSERT INTO ss_material_alias (texto_crudo, texto_crudo_norm, item_id, origen, confianza, creado_en)
            VALUES (@textoCrudo, @textoCrudoNorm, @itemId, @origen, @confianza, now())
            ON CONFLICT (texto_crudo_norm) DO NOTHING
            """;
        await conn.ExecuteAsync(sql, new { textoCrudo, textoCrudoNorm, itemId, origen, confianza = (double)confianza });
    }
}
