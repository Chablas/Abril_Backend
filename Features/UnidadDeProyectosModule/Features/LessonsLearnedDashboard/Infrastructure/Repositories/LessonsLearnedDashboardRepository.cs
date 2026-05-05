using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Infrastructure.Repositories
{
    public class LessonsLearnedDashboardRepository : ILessonsLearnedDashboardRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public LessonsLearnedDashboardRepository(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<LessonPeriodDTO>> GetAllPeriodsFactory()
        {
            using var ctx = _factory.CreateDbContext();
            return await ctx.Lesson
                .Where(l => l.State)
                .Select(l => new LessonPeriodDTO { PeriodDate = l.PeriodDate })
                .Distinct()
                .OrderByDescending(l => l.PeriodDate)
                .ToListAsync();
        }
    }
}
