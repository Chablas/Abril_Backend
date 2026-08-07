using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Repositories
{
    public class PlaneamientoBimBloqueoRepository : IPlaneamientoBimBloqueoRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public PlaneamientoBimBloqueoRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<BloqueoDto>> GetPaged(int projectId, bool? soloActivos)
        {
            using var ctx = _factory.CreateDbContext();

            var query = ctx.BimBloqueo.Where(b => b.ProjectId == projectId);
            if (soloActivos == true)
                query = query.Where(b => b.FechaCierre == null);

            return await query
                .OrderByDescending(b => b.FechaCreacion)
                .Select(b => new BloqueoDto
                {
                    Id = b.Id,
                    ProjectId = b.ProjectId,
                    Descripcion = b.Descripcion,
                    Estado = b.Estado,
                    FechaCreacion = b.FechaCreacion,
                    FechaActualizacion = b.FechaActualizacion,
                    FechaCierre = b.FechaCierre,
                })
                .ToListAsync();
        }

        public async Task<BloqueoDto> Create(int projectId, BloqueoCreateDto dto, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var existeProyecto = await ctx.Project.AnyAsync(p => p.ProjectId == projectId);
            if (!existeProyecto)
                throw new AbrilException("El proyecto no existe.", 404);

            var bloqueo = new BimBloqueo
            {
                ProjectId = projectId,
                Descripcion = dto.Descripcion.Trim(),
                Estado = dto.Estado,
                FechaCreacion = DateTimeOffset.UtcNow,
                CreatedUserId = userId,
            };

            ctx.BimBloqueo.Add(bloqueo);
            await ctx.SaveChangesAsync();

            return Map(bloqueo);
        }

        public async Task<BloqueoDto> Update(int bloqueoId, BloqueoUpdateDto dto)
        {
            using var ctx = _factory.CreateDbContext();

            var bloqueo = await ctx.BimBloqueo.FirstOrDefaultAsync(b => b.Id == bloqueoId);
            if (bloqueo == null)
                throw new AbrilException("El bloqueo no existe.", 404);
            if (bloqueo.FechaCierre != null)
                throw new AbrilException("No se puede editar un bloqueo cerrado.", 409);

            bloqueo.Descripcion = dto.Descripcion.Trim();
            bloqueo.Estado = dto.Estado;
            bloqueo.FechaActualizacion = DateTimeOffset.UtcNow;

            await ctx.SaveChangesAsync();

            return Map(bloqueo);
        }

        public async Task<BloqueoDto> Cerrar(int bloqueoId)
        {
            using var ctx = _factory.CreateDbContext();

            var bloqueo = await ctx.BimBloqueo.FirstOrDefaultAsync(b => b.Id == bloqueoId);
            if (bloqueo == null)
                throw new AbrilException("El bloqueo no existe.", 404);
            if (bloqueo.FechaCierre != null)
                throw new AbrilException("El bloqueo ya está cerrado.", 409);

            var ahora = DateTimeOffset.UtcNow;
            bloqueo.Estado = "CERRADO";
            bloqueo.FechaCierre = ahora;
            bloqueo.FechaActualizacion = ahora;

            await ctx.SaveChangesAsync();

            return Map(bloqueo);
        }

        private static BloqueoDto Map(BimBloqueo b) => new()
        {
            Id = b.Id,
            ProjectId = b.ProjectId,
            Descripcion = b.Descripcion,
            Estado = b.Estado,
            FechaCreacion = b.FechaCreacion,
            FechaActualizacion = b.FechaActualizacion,
            FechaCierre = b.FechaCierre,
        };
    }
}
