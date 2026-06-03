using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Interfaces;
using Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Services
{
    public class LessonService : ILessonService
    {
        private readonly ILessonRepository _lessonRepository;

        public LessonService(ILessonRepository lessonRepository)
        {
            _lessonRepository = lessonRepository;
        }

        public Task<LessonDetailDTO?> GetByIdAsync(int id)
            => _lessonRepository.GetByIdAsync(id);

        public Task<PagedResult<LessonListDTO>> GetLessonsFilterPaged(
            DateTimeOffset? periodDate, int? stateId, int? projectId,
            int? areaId, int? userId, int page, int pageSize)
            => _lessonRepository.GetLessonsFilterPaged(periodDate, stateId, projectId, areaId, userId, page, pageSize);

        public Task<LessonsPagedWithFiltersDTO> GetPagedWithFilters(LessonFilterDTO filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return _lessonRepository.GetPagedWithFiltersAsync(filter);
        }

        public Task<List<LessonListDTO>> GetLessonsFilterAsync(
            string? period, int? stateId, int? projectId, int? areaId, int? userId)
            => _lessonRepository.GetLessonsFilterAsync(period, stateId, projectId, areaId, userId);

        public Task<object?> CreateAsync(LessonCreateDTO dto, int userId)
            => _lessonRepository.CreateAsync(dto, userId);

        public Task<bool> DeleteSoftAsync(int lessonId, int userId)
            => _lessonRepository.DeleteSoftAsync(lessonId, userId);
    }
}
