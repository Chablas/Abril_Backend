using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedFeature.Presentation
{
    [ApiController]
    [Route("api/v1/lesson")]
    public class LessonsLearnedController : ControllerBase
    {
        private readonly ILessonService _lessonService;

        public LessonsLearnedController(ILessonService lessonService)
        {
            _lessonService = lessonService;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetLessonsUsingFilter(
            [FromQuery] DateTime? periodDate,
            [FromQuery] int? stateId,
            [FromQuery] int? projectId,
            [FromQuery] int? areaId,
            [FromQuery] int? phaseId,
            [FromQuery] int? stageId,
            [FromQuery] int? layerId,
            [FromQuery] int? subStageId,
            [FromQuery] int? subSpecialtyId,
            [FromQuery] int? userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10
        )
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _lessonService.GetLessonsFilterPaged(
                    periodDate, stateId, projectId, areaId, phaseId, stageId, layerId,
                    subStageId, subSpecialtyId, userId, page, pageSize
                );
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("paged-with-filters")]
        public async Task<IActionResult> GetPagedWithFilters(
            [FromQuery] DateTime? periodDate,
            [FromQuery] int? stateId,
            [FromQuery] int? projectId,
            [FromQuery] int? areaId,
            [FromQuery] int? phaseId,
            [FromQuery] int? stageId,
            [FromQuery] int? layerId,
            [FromQuery] int? subStageId,
            [FromQuery] int? subSpecialtyId,
            [FromQuery] int? userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10
        )
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var filter = new LessonFilterDTO
                {
                    PeriodDate = periodDate,
                    StateId = stateId,
                    ProjectId = projectId,
                    AreaId = areaId,
                    PhaseId = phaseId,
                    StageId = stageId,
                    LayerId = layerId,
                    SubStageId = subStageId,
                    SubSpecialtyId = subSpecialtyId,
                    UserId = userId,
                    Page = page,
                    PageSize = pageSize
                };

                var result = await _lessonService.GetPagedWithFilters(filter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message, stackTrace = ex.StackTrace, inner = ex.InnerException?.Message });
            }
        }
    }
}
