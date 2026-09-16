using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Dapper;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

public class CostoFijoManualRepository : ICostoFijoManualRepository
{
    private readonly IConfiguration _config;
    public CostoFijoManualRepository(IConfiguration config) => _config = config;
    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]!);

    public async Task<CostoFijoManualDto?> ObtenerPorProyectoAsync(int projectId)
    {
        using var conn = Conn();
        const string sql = """
            SELECT project_id AS ProjectId, malla_anticaida AS MallaAnticaida,
                   encapsulado AS Encapsulado, malla_anillo_fenolico AS MallaAnilloFenolico,
                   notas AS Notas
            FROM ss_presupuesto_costo_fijo_manual
            WHERE project_id = @projectId
            """;
        return await conn.QuerySingleOrDefaultAsync<CostoFijoManualDto>(sql, new { projectId });
    }

    public async Task GuardarAsync(int projectId, ActualizarCostoFijoManualDto dto)
    {
        using var conn = Conn();
        const string sql = """
            INSERT INTO ss_presupuesto_costo_fijo_manual
                (project_id, malla_anticaida, encapsulado, malla_anillo_fenolico, notas)
            VALUES (@projectId, @MallaAnticaida, @Encapsulado, @MallaAnilloFenolico, @Notas)
            ON CONFLICT (project_id) DO UPDATE SET
                malla_anticaida = EXCLUDED.malla_anticaida,
                encapsulado = EXCLUDED.encapsulado,
                malla_anillo_fenolico = EXCLUDED.malla_anillo_fenolico,
                notas = EXCLUDED.notas,
                actualizado_en = now()
            """;
        await conn.ExecuteAsync(sql, new
        {
            projectId,
            dto.MallaAnticaida,
            dto.Encapsulado,
            dto.MallaAnilloFenolico,
            dto.Notas,
        });
    }
}
