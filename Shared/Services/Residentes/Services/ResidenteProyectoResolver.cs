using Microsoft.EntityFrameworkCore;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Services.Residentes.Interfaces;

namespace Abril_Backend.Shared.Services.Residentes.Services
{
    public class ResidenteProyectoResolver : IResidenteProyectoResolver
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public ResidenteProyectoResolver(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<int>> ProyectosDelResidenteAsync(int userId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.ProyectosDelResidente(userId).ToListAsync();
        }

        public async Task<bool> EsResidenteDelProyectoAsync(int userId, int projectId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.ProyectosDelResidente(userId).AnyAsync(id => id == projectId);
        }

        public async Task<ResidenteDto?> ResidenteDelProyectoAsync(int projectId)
        {
            using var ctx = _factory.CreateDbContext();
            return await (
                from p in ctx.Project
                join w in ctx.Worker on p.ResidenteWorkersId equals (int?)w.Id
                where p.ProjectId == projectId
                select new ResidenteDto(
                    w.Id,
                    w.PersonId,
                    w.Person != null ? w.Person.UserId : null,
                    w.Person != null ? w.Person.FullName : null,
                    w.EmailCorporativo)
            ).FirstOrDefaultAsync();
        }

        public async Task<List<int>> ObrasConResidenteAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.ObrasConResidente().ToListAsync();
        }
    }
}
