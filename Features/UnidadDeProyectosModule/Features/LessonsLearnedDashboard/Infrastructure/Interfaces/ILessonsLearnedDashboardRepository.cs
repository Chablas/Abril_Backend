using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Infrastructure.Interfaces
{
    public interface ILessonsLearnedDashboardRepository
    {
        Task<List<LessonPeriodDTO>> GetAllPeriodsFactory();
    }
}
