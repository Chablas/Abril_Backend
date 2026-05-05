using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Application.Interfaces
{
    public interface ILessonsLearnedDashboardService
    {
        Task<LessonsLearnedDashboardFiltersResponseDto> GetFilters();
    }
}
