using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Application.DTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ClosedXML.Excel;
using System.IO;
using Abril_Backend.Infrastructure.InternalServices;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Controllers
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class LessonController : ControllerBase
    {
        private readonly LessonRepository _lessonRepository;
        private readonly ProjectRepository _projectRepository;
        private readonly AreaRepository _areaRepository;
        private readonly PhaseStageSubStageSubSpecialtyRepository _phaseStageSubStageSubSpecialtyRepository;
        private readonly DashboardRepository _dashboardRepository;
        private readonly ExcelService _excelService;
        private readonly PhaseRepository _phaseRepository;
        private readonly StageRepository _stageRepository;
        private readonly LayerRepository _layerRepository;
        private readonly SubStageRepository _subStageRepository;
        private readonly SubSpecialtyRepository _subSpecialtyRepository;
        private readonly UserRepository _userRepository;
        private readonly IEmailService _emailService;
        public LessonController(
            LessonRepository lessonRepository,
            ProjectRepository projectRepository,
            AreaRepository areaRepository,
            PhaseStageSubStageSubSpecialtyRepository phaseStageSubStageSubSpecialtyRepository,
            DashboardRepository dashboardRepository,
            ExcelService excelService,
            PhaseRepository phaseRepository,
            StageRepository stageRepository,
            LayerRepository layerRepository,
            SubStageRepository subStageRepository,
            SubSpecialtyRepository subSpecialtyRepository,
            UserRepository userRepository,
            IEmailService emailService
            )
        {
            _lessonRepository = lessonRepository;
            _projectRepository = projectRepository;
            _areaRepository = areaRepository;
            _phaseStageSubStageSubSpecialtyRepository = phaseStageSubStageSubSpecialtyRepository;
            _dashboardRepository = dashboardRepository;
            _excelService = excelService;
            _phaseRepository = phaseRepository;
            _stageRepository = stageRepository;
            _layerRepository = layerRepository;
            _subStageRepository = subStageRepository;
            _subSpecialtyRepository = subSpecialtyRepository;
            _userRepository = userRepository;
            _emailService = emailService;
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
        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var areasTask = _areaRepository.GetAllFactory();
                var projectsTask = _projectRepository.GetAllFactory();
                var lessonPeriodsTask = _lessonRepository.GetAllPeriodsFactory();
                var phasesTask = _phaseRepository.GetAllFactory();
                var stagesTask = _stageRepository.GetAllFactory();
                var layersTask = _layerRepository.GetAllFactory();
                var subStagesTask = _subStageRepository.GetAllFactory();
                var subSpecialtiesTask = _subSpecialtyRepository.GetAllFactory();
                var usersTask = _userRepository.GetAllUsersFactory();

                await Task.WhenAll(areasTask, projectsTask, lessonPeriodsTask, phasesTask, stagesTask, layersTask, subStagesTask, subSpecialtiesTask, usersTask);

                var result = new
                {
                    Areas = areasTask.Result,
                    Projects = projectsTask.Result,
                    Periods = lessonPeriodsTask.Result,
                    Phases = phasesTask.Result,
                    Stages = stagesTask.Result,
                    Layers = layersTask.Result,
                    SubStages = subStagesTask.Result,
                    SubSpecialties = subSpecialtiesTask.Result,
                    Users = usersTask.Result
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

                var result = await _lessonRepository.GetLessonsFilterPaged(
                    periodDate, stateId, projectId, areaId, phaseId, stageId, layerId, subStageId, subSpecialtyId, userId, page, pageSize
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
        [HttpPost("send-pdf")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SendPDF([FromForm] IFormFile pdf)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                if (pdf == null || pdf.Length == 0)
                    return BadRequest(new { message = "Debe adjuntar un PDF" });

                if (pdf.ContentType != "application/pdf")
                    return BadRequest(new { message = "El archivo debe ser un PDF" });

                byte[] fileBytes;
                using (var ms = new MemoryStream())
                {
                    await pdf.CopyToAsync(ms);
                    fileBytes = ms.ToArray();
                }

                await _emailService.SendAsync(
                    to: new List<string> { "calvarez@abril.pe" },
                    subject: "PDF mensual",
                    body: "Adjunto PDF mensual",
                    isHtml: false,
                    attachments: new List<EmailAttachment>
                {
                    new EmailAttachment
                    {
                        FileName = pdf.FileName,
                        ContentType = pdf.ContentType,
                        Content = fileBytes,
                    }
                });

                return Ok(new { message = "PDF enviado correctamente" });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
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