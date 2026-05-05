using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedFeature.Infrastructure.Interfaces
{
    public interface ILessonRepository
    {
        Task<PagedResult<LessonListDTO>> GetLessonsFilterPaged(
            DateTime? periodDate,
            int? stateId,
            int? projectId,
            int? areaId,
            int? phaseId,
            int? stageId,
            int? layerId,
            int? subStageId,
            int? subSpecialtyId,
            int? userId,
            int page,
            int pageSize
        );

        Task<LessonsPagedWithFiltersDTO> GetPagedWithFiltersAsync(LessonFilterDTO filter);
    }
}
