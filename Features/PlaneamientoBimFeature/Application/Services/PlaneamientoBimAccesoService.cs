using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Application.Services
{
    public class PlaneamientoBimAccesoService : IPlaneamientoBimAccesoService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PlaneamientoBimAccesoService(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task ValidarAccesoProyecto(int userId, int projectId, bool esAdmin, bool esPlaneamientoUdp)
        {
            if (esAdmin) return;

            if (!esPlaneamientoUdp)
                throw new AbrilException("No tiene permiso para acceder a este proyecto.", 403);

            using var ctx = _factory.CreateDbContext();

            var workerId = await ctx.Worker
                .Where(w => w.Person != null && w.Person.UserId == userId)
                .Select(w => (int?)w.Id)
                .FirstOrDefaultAsync();

            var esResponsable = workerId != null && await ctx.Project
                .AnyAsync(p => p.ProjectId == projectId && p.ResponsablePlaneamientoBimId == workerId);

            if (!esResponsable)
                throw new AbrilException("No tiene permiso para acceder a este proyecto.", 403);
        }

        public async Task<int> ResolverProjectIdDeRestriccion(int restriccionId)
        {
            using var ctx = _factory.CreateDbContext();

            var projectId = await ctx.BimRestriccion
                .Where(r => r.Id == restriccionId)
                .Select(r => (int?)r.ProjectId)
                .FirstOrDefaultAsync();

            if (projectId == null)
                throw new AbrilException("La restricción no existe.", 404);

            return projectId.Value;
        }
    }
}
