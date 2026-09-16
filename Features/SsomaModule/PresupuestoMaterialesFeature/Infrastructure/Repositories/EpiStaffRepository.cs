using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Repositories;

public class EpiStaffRepository : IEpiStaffRepository
{
    private readonly IConfiguration _config;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public EpiStaffRepository(IConfiguration config, IDbContextFactory<AppDbContext> factory)
    {
        _config = config;
        _factory = factory;
    }

    private NpgsqlConnection Conn() => new(_config["Database:PostgreSQL"]!);

    public async Task<EpiStaffConfigDto> ObtenerConfigAsync()
    {
        using var conn = Conn();
        const string sql = """
            SELECT arnes_por_staff AS ArnesPorStaff, rotacion_lentes_meses AS RotacionLentesMeses,
                   rotacion_barbiquejo_meses AS RotacionBarbiquejoMeses,
                   rotacion_guantes_meses AS RotacionGuantesMeses
            FROM ss_epi_staff_config
            LIMIT 1
            """;
        return await conn.QuerySingleAsync<EpiStaffConfigDto>(sql);
    }

    public async Task ActualizarConfigAsync(ActualizarEpiStaffConfigDto dto)
    {
        using var conn = Conn();
        // Fila única (singleton) — sin WHERE porque solo existe una fila, sembrada por la
        // migración manual (Migrations_Manual/2026-09-15_ss_epi_staff_config.sql).
        const string sql = """
            UPDATE ss_epi_staff_config
            SET arnes_por_staff = @ArnesPorStaff,
                rotacion_lentes_meses = @RotacionLentesMeses,
                rotacion_barbiquejo_meses = @RotacionBarbiquejoMeses,
                rotacion_guantes_meses = @RotacionGuantesMeses,
                actualizado_en = now()
            """;
        await conn.ExecuteAsync(sql, dto);
    }

    // Mismo criterio de "cronograma vigente" que ya usan PersonalHitoRepository/
    // VigilanciaHitoRepository para "Semanas" — la versión de historial marcada como la actual,
    // solo hitos críticos.
    private const string CronogramaVigenteCte = """
        cronograma_vigente AS (
            SELECT ms.planned_start_date, ms.planned_end_date
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

    public async Task<decimal> ObtenerMesesProyectoAsync(int projectId)
    {
        using var conn = Conn();
        // El fin del proyecto es el fin real del último hito crítico (ej. Acabados fachada
        // principal), no su fecha de inicio — un hito que empieza el 20/01 pero dura varios meses
        // más estaba cortando la duración corta antes de este fix.
        var sql = $"""
            WITH {CronogramaVigenteCte}
            SELECT (MAX(COALESCE(planned_end_date, planned_start_date)) - MIN(planned_start_date)) / 30.44
            FROM cronograma_vigente
            WHERE planned_start_date IS NOT NULL
            """;
        var meses = await conn.QuerySingleOrDefaultAsync<decimal?>(sql, new { projectId });
        return meses ?? 0;
    }

    public async Task<decimal> ObtenerAreaTechadaAsync(int projectId)
    {
        using var ctx = _factory.CreateDbContext();
        var area = await ctx.Project
            .Where(p => p.ProjectId == projectId)
            .Select(p => p.AreaTechadaM2)
            .FirstOrDefaultAsync();
        return area ?? 0;
    }

    private class PreciosStaffObreroRow
    {
        public decimal PrecioStaff { get; set; }
        public decimal PrecioObrero { get; set; }
    }

    public async Task<(decimal PrecioStaff, decimal PrecioObrero)> ObtenerPreciosCascoAsync()
    {
        using var conn = Conn();
        // 'CASCO%' (empieza con, no "contiene") — con '%CASCO%' a ambos lados esto también
        // agarraba accesorios tipo "OREJERA PARA CASCO 3M" o "BARBIQUEJO PARA CASCO", que
        // contienen la palabra "CASCO" en el medio del nombre pero no son cascos, contaminando el
        // precio promedio de la variante de obrero hacia abajo.
        const string sql = """
            SELECT
              COALESCE(AVG(precio_unitario) FILTER (
                WHERE recurso_crudo ILIKE 'CASCO%' AND (recurso_crudo ILIKE '%BLANCO%' OR recurso_crudo ILIKE '%INGENIER%')
              ), 0) AS PrecioStaff,
              COALESCE(AVG(precio_unitario) FILTER (
                WHERE recurso_crudo ILIKE 'CASCO%' AND NOT (recurso_crudo ILIKE '%BLANCO%' OR recurso_crudo ILIKE '%INGENIER%')
              ), 0) AS PrecioObrero
            FROM ss_consumo_linea
            WHERE recurso_crudo ILIKE 'CASCO%'
            """;
        var fila = await conn.QuerySingleAsync<PreciosStaffObreroRow>(sql);
        return (fila.PrecioStaff, fila.PrecioObrero);
    }

    public async Task<(decimal PrecioStaff, decimal PrecioObrero)> ObtenerPreciosOrejeraAsync()
    {
        using var conn = Conn();
        const string sql = """
            SELECT
              COALESCE(AVG(precio_unitario) FILTER (
                WHERE recurso_crudo ILIKE '%OREJERA%' AND recurso_crudo ILIKE '%3M%'
              ), 0) AS PrecioStaff,
              COALESCE(AVG(precio_unitario) FILTER (
                WHERE recurso_crudo ILIKE '%OREJERA%' AND recurso_crudo NOT ILIKE '%3M%'
              ), 0) AS PrecioObrero
            FROM ss_consumo_linea
            WHERE recurso_crudo ILIKE '%OREJERA%'
            """;
        var fila = await conn.QuerySingleAsync<PreciosStaffObreroRow>(sql);
        return (fila.PrecioStaff, fila.PrecioObrero);
    }
}
