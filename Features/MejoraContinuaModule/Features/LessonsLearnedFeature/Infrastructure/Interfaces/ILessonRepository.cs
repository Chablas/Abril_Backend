using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Infrastructure.Interfaces
{
    public interface ILessonRepository
    {
        Task<PagedResult<LessonListDTO>> GetLessonsFilterPaged(
            DateTimeOffset? periodDate,
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

        Task<object?> CreateAsync(LessonCreateDTO dto, int userId);

        Task<List<PhaseStageSubStageSubSpecialtyDTO>> GetFiltersForCreateAsync(int areaId, int? subAreaId);

        // Migrados desde el repo legacy
        Task<LessonDetailDTO?> GetByIdAsync(int id);
        Task<List<LessonListDTO>> GetLessonsFilterAsync(
            string? period,
            int? stateId,
            int? projectId,
            int? areaId,
            int? phaseId,
            int? stageId,
            int? layerId,
            int? subStageId,
            int? subSpecialtyId);
        Task<bool> DeleteSoftAsync(int lessonId, int userId);
    }
}
