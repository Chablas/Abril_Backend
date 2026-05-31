using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Evaluaciones.Infrastructure.Repositories
{
    public class EvPeriodoRepository : IEvPeriodoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public EvPeriodoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<EvPeriodo?> GetActivoAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvPeriodos.FirstOrDefaultAsync(p => p.Activo);
        }

        public async Task<List<EvPeriodo>> GetAllAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvPeriodos
                .OrderByDescending(p => p.Anio)
                .ThenByDescending(p => p.Mes)
                .ToListAsync();
        }

        public async Task<EvPeriodo?> GetByIdAsync(int id)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.EvPeriodos.FindAsync(id);
        }

        public async Task<EvPeriodo> CreateAsync(EvPeriodo periodo)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.EvPeriodos.Add(periodo);
            await ctx.SaveChangesAsync();
            return periodo;
        }

        public async Task UpdateAsync(EvPeriodo periodo)
        {
            using var ctx = _factory.CreateDbContext();
            ctx.EvPeriodos.Update(periodo);
            await ctx.SaveChangesAsync();
        }
    }
}
