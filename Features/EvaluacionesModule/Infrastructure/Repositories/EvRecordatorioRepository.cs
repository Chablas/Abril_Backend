using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Repositories
{
    public class EvRecordatorioRepository : IEvRecordatorioRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EvRecordatorioRepository(IDbContextFactory<AppDbContext> factory)
            => _factory = factory;

        public async Task<EvPeriodo?> GetPeriodoActivoAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvPeriodos.FirstOrDefaultAsync(p => p.Activo);
        }

        public async Task<EvPeriodo?> GetPeriodoCerradoAyerAsync()
        {
            using var ctx = _factory.CreateDbContext();
            var ayer = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            return await ctx.EvPeriodos
                .FirstOrDefaultAsync(p => !p.Activo && p.FechaCierre == ayer);
        }

        public async Task<List<EvaluadorDto>> GetEvaluadoresPendientesAsync(int periodoId, bool soloSinEvaluar)
        {
            using var ctx = _factory.CreateDbContext();
            await ctx.Database.OpenConnectionAsync();
            var conn = ctx.Database.GetDbConnection();

            var subAreaMapeo = @"
                CASE w.subarea
                    WHEN 'Unidad de Proyectos'    THEN 'Jefe de Proyectos'
                    WHEN 'Ingeniería BIM'          THEN 'Jefe de Proyectos'
                    WHEN 'Planeamiento BIM'        THEN 'Jefe de Proyectos'
                    WHEN 'SSOMA'                   THEN 'Jefe de Seguridad y Salud en el trabajo'
                    WHEN 'Arquitectura'            THEN 'Jefe de Arquitectura'
                    WHEN 'Arquitectura Comercial'  THEN 'Jefe de Arquitectura Comercial'
                    WHEN 'Calidad'                 THEN 'Jefe de Calidad'
                    WHEN 'Costos y Presupuestos'   THEN 'Jefe de Costos y Presupuestos'
                    WHEN 'Post Venta'              THEN 'Jefe de Post Venta'
                    WHEN 'Producción'              THEN 'Gerente De Proyectos'
                    WHEN 'Administración Obra'     THEN 'Gerente De Proyectos'
                    ELSE NULL
                END";

            var sql = $@"
                SELECT DISTINCT
                    au.user_id          AS UserId,
                    p.full_name         AS NombreCompleto,
                    w.email_personal    AS EmailPersonal,
                    w.subarea           AS Subarea,
                    cj.email            AS JefeEmail,
                    cj.nombre           AS JefeNombre
                FROM workers w
                JOIN person p ON p.person_id = w.person_id
                LEFT JOIN app_user au ON LOWER(au.email) = LOWER(w.email_personal)
                LEFT JOIN cat_jefatura cj ON cj.nombre = ({subAreaMapeo})
                    AND cj.activo = true
                WHERE w.area = 'Proyectos'
                  AND w.subarea NOT IN ('Residencia','Almacenero','Proyectos')
                  AND w.subarea IS NOT NULL
                  AND w.subarea != ''
                  AND w.email_personal IS NOT NULL
                  AND w.email_personal != ''
                  AND (w.fecha_retiro IS NULL OR w.fecha_retiro > CURRENT_DATE)
                  {(soloSinEvaluar ? @"AND NOT EXISTS (
                      SELECT 1 FROM ev_evaluacion_residente er
                      WHERE er.periodo_id = @PeriodoId
                        AND er.evaluador_user_id = au.user_id
                  )" : "")}
                ORDER BY w.subarea, p.full_name";

            var result = await conn.QueryAsync<EvaluadorDto>(sql, new { PeriodoId = periodoId });
            return result.ToList();
        }

        public async Task RegistrarLogAsync(int periodoId, int? userId, string tipo, string emailDestino, bool ccJefatura, bool ccGerencia)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.EvRecordatorioLogs.Add(new EvRecordatorioLog
            {
                PeriodoId = periodoId,
                UserId = userId,
                Tipo = tipo,
                EmailDestino = emailDestino,
                CcJefatura = ccJefatura,
                CcGerencia = ccGerencia,
                EnviadoAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }

        public async Task<bool> YaEnvioRecordatorioHoyAsync(int periodoId, int? userId, string tipo)
        {
            using var ctx = _factory.CreateDbContext();
            var hoyUtc = DateTime.UtcNow.Date;
            return await ctx.EvRecordatorioLogs.AnyAsync(r =>
                r.PeriodoId == periodoId &&
                r.UserId == userId &&
                r.Tipo == tipo &&
                r.EnviadoAt >= hoyUtc &&
                r.EnviadoAt < hoyUtc.AddDays(1));
        }
    }
}
