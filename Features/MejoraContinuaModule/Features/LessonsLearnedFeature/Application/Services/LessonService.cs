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

        public async Task<PagedResult<LessonListDTO>> GetLessonsFilterPaged(
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
        )
        {
            return await _lessonRepository.GetLessonsFilterPaged(
                periodDate, stateId, projectId, areaId, phaseId, stageId, layerId,
                subStageId, subSpecialtyId, userId, page, pageSize
            );
        }

        public async Task<LessonsPagedWithFiltersDTO> GetPagedWithFilters(LessonFilterDTO filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return await _lessonRepository.GetPagedWithFiltersAsync(filter);
        }

        public async Task<object?> CreateAsync(LessonCreateDTO dto, int userId)
        {
            return await _lessonRepository.CreateAsync(dto, userId);
        }

        public async Task<List<PhaseStageSubStageSubSpecialtyDTO>> GetFiltersForCreateAsync(int areaId, int? subAreaId)
        {
            return await _lessonRepository.GetFiltersForCreateAsync(areaId, subAreaId);
        }

        public Task<LessonDetailDTO?> GetByIdAsync(int id)
            => _lessonRepository.GetByIdAsync(id);

        public Task<List<LessonListDTO>> GetLessonsFilterAsync(
            string? period, int? stateId, int? projectId, int? areaId,
            int? phaseId, int? stageId, int? layerId, int? subStageId, int? subSpecialtyId)
            => _lessonRepository.GetLessonsFilterAsync(
                period, stateId, projectId, areaId, phaseId, stageId, layerId, subStageId, subSpecialtyId);

        public Task<bool> DeleteSoftAsync(int lessonId, int userId)
            => _lessonRepository.DeleteSoftAsync(lessonId, userId);
    }
}
