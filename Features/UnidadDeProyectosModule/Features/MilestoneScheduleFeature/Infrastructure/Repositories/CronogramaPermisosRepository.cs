using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Residentes.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Constants;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Repositories
{
    public class CronogramaPermisosRepository : ICronogramaPermisosRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly IResidenteProyectoResolver _residentes;

        public CronogramaPermisosRepository(IDbContextFactory<AppDbContext> factory, IResidenteProyectoResolver residentes)
        {
            _factory = factory;
            _residentes = residentes;
        }

        /// <summary>La regla (cruzar por persona, con la ficha viva) vive en el resolver compartido.</summary>
        public Task<bool> EsResidenteDelProyectoAsync(int userId, int projectId)
            => _residentes.EsResidenteDelProyectoAsync(userId, projectId);

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

        /// <summary>Primero el residente, que es quien edita casi siempre (una consulta); si no lo
        /// es, la feature de administrar. El residente sale del resolver para no repetir la regla
        /// en SQL crudo.</summary>
        public async Task<bool> PuedeEditarProyectoAsync(int userId, int[] roleIds, bool esResidente, int projectId)
            => (esResidente && await _residentes.EsResidenteDelProyectoAsync(userId, projectId))
               || await AdministraAsync(roleIds);
    }
}
