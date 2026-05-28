using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Abril_Backend.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Repositories
{
    public class CronogramaActividadesRepository : ICronogramaActividadesRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public CronogramaActividadesRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<ProyectoSimpleCronogramaDto>> GetProyectosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Project
                .Where(p => p.State && p.TieneUnidadDeProyectos &&
                            ctx.ProjectActivity.Any(a => a.ProjectId == p.ProjectId && a.State && a.Active))
                .OrderBy(p => p.ProjectDescription)
                .Select(p => new ProyectoSimpleCronogramaDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    ResponsableUdp = p.ResponsableUdp
                })
                .ToListAsync();
        }

        public async Task<List<ActividadDto>> GetActividadesAsync(int proyectoId)
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State && a.Active)
                .OrderBy(a => a.Order)
                .Select(a => new ActividadDto
                {
                    ProjectActivityId = a.ProjectActivityId,
                    ProjectId = a.ProjectId,
                    ActivityDescription = a.ActivityDescription,
                    PlannedStartDate = a.PlannedStartDate,
                    PlannedEndDate = a.PlannedEndDate,
                    ActualEndDate = a.ActualEndDate,
                    ProgressPercentage = a.ProgressPercentage,
                    Order = a.Order
                })
                .ToListAsync();
        }

        public async Task<ActividadDto> CrearActividadAsync(int proyectoId, CrearActividadRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var maxOrder = await ctx.ProjectActivity
                .Where(a => a.ProjectId == proyectoId && a.State)
                .Select(a => (int?)a.Order)
                .MaxAsync() ?? 0;

            var activity = new ProjectActivity
            {
                ProjectId = proyectoId,
                ActivityDescription = request.ActivityDescription,
                PlannedStartDate = request.PlannedStartDate,
                PlannedEndDate = request.PlannedEndDate,
                ActualEndDate = null,
                ProgressPercentage = request.ProgressPercentage,
                Order = maxOrder + 1,
                CreatedDateTime = DateTime.UtcNow,
                CreatedUserId = userId,
                Active = true,
                State = true
            };
            ctx.ProjectActivity.Add(activity);
            await ctx.SaveChangesAsync();

            return new ActividadDto
            {
                ProjectActivityId = activity.ProjectActivityId,
                ProjectId = activity.ProjectId,
                ActivityDescription = activity.ActivityDescription,
                PlannedStartDate = activity.PlannedStartDate,
                PlannedEndDate = activity.PlannedEndDate,
                ActualEndDate = activity.ActualEndDate,
                ProgressPercentage = activity.ProgressPercentage,
                Order = activity.Order
            };
        }

        public async Task<ActividadDto> EditarActividadAsync(int projectActivityId, EditarActividadRequest request, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var activity = await ctx.ProjectActivity
                .FirstOrDefaultAsync(a => a.ProjectActivityId == projectActivityId && a.State);
            if (activity == null)
                throw new AbrilException("Actividad no encontrada.", 404);

            activity.ActivityDescription = request.ActivityDescription;
            activity.PlannedStartDate = request.PlannedStartDate;
            activity.PlannedEndDate = request.PlannedEndDate;
            activity.ActualEndDate = request.ActualEndDate;
            activity.ProgressPercentage = request.ProgressPercentage;
            activity.UpdatedDateTime = DateTime.UtcNow;
            activity.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();

            return new ActividadDto
            {
                ProjectActivityId = activity.ProjectActivityId,
                ProjectId = activity.ProjectId,
                ActivityDescription = activity.ActivityDescription,
                PlannedStartDate = activity.PlannedStartDate,
                PlannedEndDate = activity.PlannedEndDate,
                ActualEndDate = activity.ActualEndDate,
                ProgressPercentage = activity.ProgressPercentage,
                Order = activity.Order
            };
        }

        public async Task<CulminarActividadDto> CulminarActividadAsync(int projectActivityId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var activity = await ctx.ProjectActivity
                .FirstOrDefaultAsync(a => a.ProjectActivityId == projectActivityId && a.State);
            if (activity == null)
                throw new AbrilException("Actividad no encontrada.", 404);

            if (activity.ActualEndDate.HasValue)
            {
                activity.ActualEndDate = null;
                activity.ProgressPercentage = 0;
            }
            else
            {
                activity.ActualEndDate = DateOnly.FromDateTime(DateTime.UtcNow);
                activity.ProgressPercentage = 100;
            }
            activity.UpdatedDateTime = DateTime.UtcNow;
            activity.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();

            return new CulminarActividadDto
            {
                ProjectActivityId = activity.ProjectActivityId,
                ActualEndDate = activity.ActualEndDate,
                ProgressPercentage = activity.ProgressPercentage
            };
        }

        public async Task<List<DebugProyectoDto>> GetDebugProyectosAsync()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Project
                .OrderBy(p => p.ProjectId)
                .Select(p => new DebugProyectoDto
                {
                    ProjectId = p.ProjectId,
                    ProjectDescription = p.ProjectDescription,
                    TieneUnidadDeProyectos = p.TieneUnidadDeProyectos,
                    State = p.State
                })
                .ToListAsync();
        }

        public async Task EliminarActividadAsync(int projectActivityId, int userId)
        {
            using var ctx = _factory.CreateDbContext();

            var activity = await ctx.ProjectActivity
                .FirstOrDefaultAsync(a => a.ProjectActivityId == projectActivityId && a.State);
            if (activity == null)
                throw new AbrilException("Actividad no encontrada.", 404);

            activity.State = false;
            activity.Active = false;
            activity.UpdatedDateTime = DateTime.UtcNow;
            activity.UpdatedUserId = userId;

            await ctx.SaveChangesAsync();
        }
    }
}
