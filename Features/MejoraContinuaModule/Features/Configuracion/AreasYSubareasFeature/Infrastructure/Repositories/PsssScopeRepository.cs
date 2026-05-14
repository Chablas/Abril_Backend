using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.AreasYSubareasFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.AreasYSubareasFeature.Infrastructure.Repositories
{
    public class PsssScopeRepository : IPsssScopeRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PsssScopeRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<int>> GetByAreaAsync(int areaId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.PsssScope
                .Where(s => s.AreaId == areaId && s.SubAreaId == null && s.State)
                .Select(s => s.PhaseStageSubStageSubSpecialtyId)
                .ToListAsync();
        }

        public async Task<List<int>> GetBySubAreaAsync(int subAreaId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.PsssScope
                .Where(s => s.SubAreaId == subAreaId && s.State)
                .Select(s => s.PhaseStageSubStageSubSpecialtyId)
                .ToListAsync();
        }

        public async Task UpdateByAreaAsync(int areaId, List<int> psssIds)
        {
            using var ctx = _factory.CreateDbContext();

            var existing = await ctx.PsssScope
                .Where(s => s.AreaId == areaId && s.SubAreaId == null)
                .ToListAsync();
            ctx.PsssScope.RemoveRange(existing);

            var newScopes = psssIds.Distinct().Select(id => new PsssScope
            {
                PhaseStageSubStageSubSpecialtyId = id,
                AreaId = areaId,
                SubAreaId = null,
                State = true
            }).ToList();

            ctx.PsssScope.AddRange(newScopes);
            await ctx.SaveChangesAsync();
        }

        public async Task UpdateBySubAreaAsync(int subAreaId, List<int> psssIds)
        {
            using var ctx = _factory.CreateDbContext();

            var existing = await ctx.PsssScope
                .Where(s => s.SubAreaId == subAreaId)
                .ToListAsync();
            ctx.PsssScope.RemoveRange(existing);

            var newScopes = psssIds.Distinct().Select(id => new PsssScope
            {
                PhaseStageSubStageSubSpecialtyId = id,
                AreaId = null,
                SubAreaId = subAreaId,
                State = true
            }).ToList();

            ctx.PsssScope.AddRange(newScopes);
            await ctx.SaveChangesAsync();
        }
    }
}
