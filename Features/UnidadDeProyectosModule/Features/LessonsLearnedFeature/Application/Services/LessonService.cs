using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedFeature.Application.Services
{
    public class LessonService : ILessonService
    {
        private readonly ILessonRepository _lessonRepository;

        public LessonService(ILessonRepository lessonRepository)
        {
            _lessonRepository = lessonRepository;
        }

        public async Task<PagedResult<LessonListDTO>> GetLessonsFilterPaged(
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
        )
        {
            return await _lessonRepository.GetLessonsFilterPaged(
                periodDate, stateId, projectId, areaId, phaseId, stageId, layerId,
                subStageId, subSpecialtyId, userId, page, pageSize
            );
        }

        public async Task<LessonsPagedWithFiltersDTO> GetPagedWithFilters(LessonFilterDTO filter)
        {
            // Combina paginación + filtros en una sola llamada al repositorio
            // (un único roundtrip a la BD usando QueryMultiple).
            if (filter.Page < 1) filter.Page = 1;
            return await _lessonRepository.GetPagedWithFiltersAsync(filter);
        }
    }
}
