using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Constants;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Repositories
{
    public class CronogramaPermisosRepository : ICronogramaPermisosRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CronogramaPermisosRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        /// <summary>Se compara la persona de la ficha que apunta el proyecto con la del usuario:
        /// una persona puede tener varias fichas (reingresos) y el proyecto guarda una sola.</summary>
        public async Task<bool> EsResidenteDelProyectoAsync(int userId, int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            return await (
                from p in ctx.Project
                join w in ctx.Worker on p.ResidenteWorkersId equals (int?)w.Id
                join pe in ctx.Person on w.PersonId equals (int?)pe.PersonId
                where p.ProjectId == projectId && pe.UserId == userId
                select p.ProjectId
            ).AnyAsync();
        }

        public async Task<bool> AdministraAsync(int[] roleIds)
        {
            if (roleIds.Length == 0) return false;

            using var ctx = _factory.CreateDbContext();

            return await ctx.Database.SqlQuery<int>($"""
                SELECT 1 AS "Value"
                FROM role_feature rf
                JOIN feature f ON f.feature_id = rf.feature_id
                WHERE rf.role_id = ANY({roleIds})
                  AND f.feature_key = {CronogramaHitosFeatures.Administrar}
                LIMIT 1
                """)
                .AnyAsync();
        }

        /// <summary>El SQL crudo no pasa por el filtro global de Worker (<c>w.State</c>): se repite a
        /// mano para que una ficha eliminada no cuente, igual que en el listado de tarjetas.</summary>
        public async Task<bool> PuedeEditarProyectoAsync(int userId, int[] roleIds, bool esResidente, int projectId)
        {
            using var ctx = _factory.CreateDbContext();

            var resultado = await ctx.Database.SqlQuery<int>($"""
                SELECT CASE
                    WHEN EXISTS (
                            SELECT 1
                            FROM role_feature rf
                            JOIN feature f ON f.feature_id = rf.feature_id
                            WHERE rf.role_id = ANY({roleIds})
                              AND f.feature_key = {CronogramaHitosFeatures.Administrar})
                      OR ({esResidente} AND EXISTS (
                            SELECT 1
                            FROM project p
                            JOIN workers w ON w.id = p.residente_workers_id AND w.state
                            JOIN person pe ON pe.person_id = w.person_id
                            WHERE p.project_id = {projectId}
                              AND pe.user_id = {userId}))
                    THEN 1
                    ELSE 0
                END AS "Value"
                """)
                .ToListAsync();

            return resultado.FirstOrDefault() == 1;
        }
    }
}
