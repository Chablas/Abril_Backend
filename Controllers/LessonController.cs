using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Application.DTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ClosedXML.Excel;
using System.IO;
using Abril_Backend.Infrastructure.InternalServices;

namespace Abril_Backend.Controllers
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class LessonController : ControllerBase
    {
        LessonRepository _lessonRepository;
        ProjectRepository _projectRepository;
        AreaRepository _areaRepository;
        PhaseStageSubStageSubSpecialtyRepository _phaseStageSubStageSubSpecialtyRepository;
        DashboardRepository _dashboardRepository;
        ExcelService _excelService;
        public LessonController(
            LessonRepository lessonRepository,
            ProjectRepository projectRepository,
            AreaRepository areaRepository,
            PhaseStageSubStageSubSpecialtyRepository phaseStageSubStageSubSpecialtyRepository,
            DashboardRepository dashboardRepository,
            ExcelService excelService
            )
        {
            _lessonRepository = lessonRepository;
            _projectRepository = projectRepository;
            _areaRepository = areaRepository;
            _phaseStageSubStageSubSpecialtyRepository = phaseStageSubStageSubSpecialtyRepository;
            _dashboardRepository = dashboardRepository;
            _excelService = excelService;
        }

        /*[HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var data = await _lessonRepository.GetAll();
            return Ok(data);
        }*/

        [Authorize]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] LessonCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                var lessonId = await _lessonRepository.Create(dto, userId);
                if (lessonId == null)
                {
                    return BadRequest(new { message = "Escoger una relación válida" });
                }

                return Ok(new { message = "Lección creada exitosamente" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("filters/initialLoad")]
        public async Task<IActionResult> GetFiltersInitialLoad()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var projects = await _projectRepository.GetAll();
                var areas = await _areaRepository.GetAll();

                var result = new LessonFiltersDTO
                {
                    Projects = projects,
                    Areas = areas
                };
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetLessonsUsingFilter(
            [FromQuery] string? period,
            [FromQuery] int? stateId,
            [FromQuery] int? projectId,
            [FromQuery] int? areaId,
            [FromQuery] int? phaseId,
            [FromQuery] int? stageId,
            [FromQuery] int? layerId,
            [FromQuery] int? subStageId,
            [FromQuery] int? subSpecialtyId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10
        )
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _lessonRepository.GetLessonsFilterPaged(
                    period, stateId, projectId, areaId, phaseId, stageId, layerId, subStageId, subSpecialtyId, page, pageSize
                );

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("all")]
        public async Task<IActionResult> GetLessonsFilter(
            [FromQuery] string? period,
            [FromQuery] int? stateId,
            [FromQuery] int? projectId,
            [FromQuery] int? areaId,
            [FromQuery] int? phaseId,
            [FromQuery] int? stageId,
            [FromQuery] int? layerId,
            [FromQuery] int? subStageId,
            [FromQuery] int? subSpecialtyId
        )
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _lessonRepository.GetLessonsFilter(
                    period, stateId, projectId, areaId, phaseId, stageId, layerId, subStageId, subSpecialtyId
                );

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var data = await _lessonRepository.GetById(id);
                if (data == null)
                {
                    return NotFound(new { message = "Lección no encontrada" });
                }
                return Ok(data);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                if (page < 1)
                    page = 1;

                var result = await _lessonRepository.GetPaged(page);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                var result = await _lessonRepository.DeleteSoftAsync(id, userId);
                if (!result)
                    return NotFound(new { message = "Lección no encontrada." });
                return Ok(new { message = "Lección eliminada exitosamente." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("filters/create")]
        public async Task<IActionResult> GetFiltersCreate()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var data = await _phaseStageSubStageSubSpecialtyRepository.GetAll();

                if (data == null || !data.Any())
                    return NoContent();

                return Ok(data);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost("dashboard")]
        public async Task<IActionResult> GetDashboardData([FromBody] List<int> subStageIds)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                if (subStageIds == null || !subStageIds.Any())
                    return BadRequest(new { message = "Debe escoger al menos una subetapa" });

                var lessonsByPhaseTask = _dashboardRepository.GetLessonsByPhase();
                var lessonsByProjectTask = _dashboardRepository.GetLessonsByProject();
                var lessonsByPhaseStageTask = _dashboardRepository.GetLessonsByPhaseAndStage();
                var lessonsBySubStage = _dashboardRepository.GetLessonsBySubStage(subStageIds);

                await Task.WhenAll(lessonsByPhaseTask, lessonsByProjectTask, lessonsByPhaseStageTask, lessonsBySubStage);

                var response = new DashboardDTO
                {
                    LessonsByPhase = await lessonsByPhaseTask,
                    LessonsByProject = await lessonsByProjectTask,
                    LessonsByPhaseAndStage = await lessonsByPhaseStageTask,
                    LessonsBySubStage = await lessonsBySubStage
                };

                return Ok(response);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("export-excel")]
        public async Task<IActionResult> ExportExcel([FromQuery] string? period,
            [FromQuery] int? stateId,
            [FromQuery] int? projectId,
            [FromQuery] int? areaId,
            [FromQuery] int? phaseId,
            [FromQuery] int? stageId,
            [FromQuery] int? layerId,
            [FromQuery] int? subStageId,
            [FromQuery] int? subSpecialtyId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var lessons = await _lessonRepository.GetLessonsFilter(
                    period, stateId, projectId, areaId, phaseId, stageId, layerId, subStageId, subSpecialtyId
                );

                var fileBytes = await _excelService.GenerateLessonsExcel(lessons);

                return File(
                    fileBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Lecciones_Aprendidas.xlsx"
                );
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /*
        [HttpPatch("[action]")]
        [Consumes("multipart/form-data")]
        public IActionResult Patch([FromForm] UpdateLessonDTO dto) {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var updated = _repository.Update(dto);
            if (!updated)
                return NotFound("La lección no existe");

            return Ok(new { message = "Lección actualizada correctamente" });
        }
        */
    }
}