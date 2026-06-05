using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Infrastructure.Interfaces
{
    public interface ILessonRepository
    {
        Task<LessonDetailDTO?> GetByIdAsync(int id, int currentUserId);

        Task<PagedResult<LessonListDTO>> GetLessonsFilterPaged(
            DateTimeOffset? periodDate,
            int? stateId,
            int? projectId,
            int? areaId,
            int? userId,
            int page,
            int pageSize);

        Task<LessonsPagedWithFiltersDTO> GetPagedWithFiltersAsync(LessonFilterDTO filter);

        Task<List<LessonListDTO>> GetLessonsFilterAsync(
            string? period,
            int? stateId,
            int? projectId,
            int? areaId,
            int? userId);

        Task<object?> CreateAsync(LessonCreateDTO dto, int userId);
        Task<bool> UpdateAsync(int lessonId, LessonUpdateDTO dto, int currentUserId);
        Task<bool> DeleteSoftAsync(int lessonId, int userId);

        Task<LessonReviewResultDTO> ApproveAsync(int lessonId, int currentUserId);
        Task<LessonReviewResultDTO> RejectAsync(int lessonId, int currentUserId, string? comment);
    }
}
