using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Infrastructure.Repositories {
    public class DashboardRepository {
        private readonly AppDbContext _context;
        private readonly IDbContextFactory<AppDbContext> _factory;
        public DashboardRepository(AppDbContext contexto, IDbContextFactory<AppDbContext> factory) {
            _context = contexto;
            _factory = factory;
        }
        public async Task<List<ChartItemDTO>> GetLessonsByPhase(DateTime? periodDate, int? userId)
        {
            await using var context = await _factory.CreateDbContextAsync();

            var query =
                from lesson in context.Lesson
                join psss in context.PhaseStageSubStageSubSpecialty
                    on lesson.PhaseStageSubStageSubSpecialtyId equals psss.PhaseStageSubStageSubSpecialtyId
                join phase in context.Phase
                    on psss.PhaseId equals phase.PhaseId
                where lesson.Active && lesson.State
                select new { lesson, phase };

            if (periodDate.HasValue)
            {
                var startOfMonth = new DateTime(periodDate.Value.Year, periodDate.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var startOfNextMonth = startOfMonth.AddMonths(1);

                query = query.Where(x =>
                    x.lesson.PeriodDate != null &&
                    x.lesson.PeriodDate >= startOfMonth &&
                    x.lesson.PeriodDate < startOfNextMonth
                );
            }

            if (userId.HasValue)
            {
                query = query.Where(x =>
                    x.lesson.CreatedUserId == userId.Value
                );
            }

            var result = await query
                .GroupBy(x => new
                {
                    x.phase.PhaseId,
                    x.phase.PhaseDescription
                })
                .Select(g => new ChartItemDTO
                {
                    Id = g.Key.PhaseId,
                    Label = g.Key.PhaseDescription,
                    Value = g.Count()
                })
                .ToListAsync();

            return result;
        }
        public async Task<List<ChartItemDTO>> GetLessonsBySubStage(
            List<int> subStageIds,
            DateTime? periodDate,
            int? userId)
        {
            await using var context = await _factory.CreateDbContextAsync();

            if (subStageIds == null || !subStageIds.Any())
                return new List<ChartItemDTO>();

            var query =
                from lesson in context.Lesson
                join psss in context.PhaseStageSubStageSubSpecialty
                    on lesson.PhaseStageSubStageSubSpecialtyId equals psss.PhaseStageSubStageSubSpecialtyId
                join subStage in context.SubStage
                    on psss.SubStageId equals subStage.SubStageId
                where lesson.Active
                      && lesson.State
                      && subStageIds.Contains(subStage.SubStageId)
                select new { lesson, subStage };

            // 🔹 Filtro por mes
            if (periodDate.HasValue)
            {
                var startOfMonth = new DateTime(
                    periodDate.Value.Year,
                    periodDate.Value.Month,
                    1, 0, 0, 0,
                    DateTimeKind.Utc
                );

                var startOfNextMonth = startOfMonth.AddMonths(1);

                query = query.Where(x =>
                    x.lesson.PeriodDate != null &&
                    x.lesson.PeriodDate >= startOfMonth &&
                    x.lesson.PeriodDate < startOfNextMonth
                );
            }

            // 🔹 Filtro por usuario
            if (userId.HasValue)
            {
                query = query.Where(x =>
                    x.lesson.CreatedUserId == userId.Value
                );
            }

            var result = await query
                .GroupBy(x => new
                {
                    x.subStage.SubStageId,
                    x.subStage.SubStageDescription
                })
                .Select(g => new ChartItemDTO
                {
                    Id = g.Key.SubStageId,
                    Label = g.Key.SubStageDescription!,
                    Value = g.Count()
                })
                .ToListAsync();

            return result;
        }
        public async Task<List<ChartItemDTO>> GetLessonsByProject(DateTime? periodDate, int? userId)
        {
            await using var context = await _factory.CreateDbContextAsync();

            var query =
                from lesson in context.Lesson
                join project in context.Project
                    on lesson.ProjectId equals project.ProjectId
                where lesson.Active && lesson.State
                select new { lesson, project };

            // 🔹 Filtro por mes
            if (periodDate.HasValue)
            {
                var startOfMonth = new DateTime(
                    periodDate.Value.Year,
                    periodDate.Value.Month,
                    1, 0, 0, 0,
                    DateTimeKind.Utc
                );

                var startOfNextMonth = startOfMonth.AddMonths(1);

                query = query.Where(x =>
                    x.lesson.PeriodDate != null &&
                    x.lesson.PeriodDate >= startOfMonth &&
                    x.lesson.PeriodDate < startOfNextMonth
                );
            }

            // 🔹 Filtro por usuario
            if (userId.HasValue)
            {
                query = query.Where(x =>
                    x.lesson.CreatedUserId == userId.Value
                );
            }

            var result = await query
                .GroupBy(x => new
                {
                    x.project.ProjectId,
                    x.project.ProjectDescription
                })
                .Select(g => new ChartItemDTO
                {
                    Id = g.Key.ProjectId,
                    Label = g.Key.ProjectDescription,
                    Value = g.Count()
                })
                .ToListAsync();

            return result;
        }
        public async Task<List<PhaseStageChartDTO>> GetLessonsByPhaseAndStage(DateTime? periodDate, int? userId)
        {
            await using var context = await _factory.CreateDbContextAsync();

            var query =
                from lesson in context.Lesson
                join psss in context.PhaseStageSubStageSubSpecialty
                    on lesson.PhaseStageSubStageSubSpecialtyId equals psss.PhaseStageSubStageSubSpecialtyId
                join stage in context.Stage
                    on psss.StageId equals stage.StageId
                where lesson.Active && lesson.State
                select new { lesson, psss, stage };

            // 🔹 Filtro por mes
            if (periodDate.HasValue)
            {
                var startOfMonth = new DateTime(
                    periodDate.Value.Year,
                    periodDate.Value.Month,
                    1, 0, 0, 0,
                    DateTimeKind.Utc
                );

                var startOfNextMonth = startOfMonth.AddMonths(1);

                query = query.Where(x =>
                    x.lesson.PeriodDate != null &&
                    x.lesson.PeriodDate >= startOfMonth &&
                    x.lesson.PeriodDate < startOfNextMonth
                );
            }

            // 🔹 Filtro por usuario
            if (userId.HasValue)
            {
                query = query.Where(x =>
                    x.lesson.CreatedUserId == userId.Value
                );
            }

            // 🔹 Agrupación después de aplicar filtros
            var lessonCountsList = await query
                .GroupBy(x => new
                {
                    x.psss.PhaseId,
                    x.stage.StageId,
                    x.stage.StageDescription
                })
                .Select(g => new
                {
                    g.Key.PhaseId,
                    g.Key.StageId,
                    g.Key.StageDescription,
                    Count = g.Count()
                })
                .ToListAsync();

            var phases = await context.Phase
                .OrderBy(p => p.Order ?? int.MaxValue)
                .Select(p => new
                {
                    p.PhaseId,
                    p.PhaseDescription,
                    p.Order
                })
                .ToListAsync();

            var result = phases
                .Select(phase => new PhaseStageChartDTO
                {
                    PhaseId = phase.PhaseId,
                    PhaseLabel = phase.PhaseDescription,
                    Stages = lessonCountsList
                        .Where(l => l.PhaseId == phase.PhaseId)
                        .Select(s => new ChartItemDTO
                        {
                            Id = s.StageId,
                            Label = s.StageDescription,
                            Value = s.Count
                        })
                        .ToList()
                })
                .ToList();

            return result;
        }
    }
}