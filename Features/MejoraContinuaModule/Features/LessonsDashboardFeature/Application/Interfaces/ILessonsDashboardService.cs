using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsDashboardFeature.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsDashboardFeature.Application.Interfaces
{
    public interface ILessonsDashboardService
    {
        Task<LessonsDashboardDataDTO> GetData(DateTimeOffset? periodDate, int? userId, int? lessonAreaId, List<int>? projectIds);
        Task<LessonsDashboardFiltersDTO> GetFilters();
        Task SendPdfAsync(IFormFile pdf);
    }
}
