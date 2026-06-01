using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Application.DTOs;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Application.Exceptions;

namespace Abril_Backend.Controllers
{
    /// <summary>
    /// Controlador legacy que ahora SOLO contiene endpoints relacionados al dashboard
    /// de lecciones aprendidas (envío de PDF + datos agregados). El resto de los
    /// endpoints fueron migrados a
    /// Features/MejoraContinuaModule/Features/LessonsLearnedFeature/Presentation/LessonsLearnedController.cs.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    public class LessonController : ControllerBase
    {
        private readonly DashboardRepository _dashboardRepository;
        private readonly IEmailService _emailService;

        public LessonController(
            DashboardRepository dashboardRepository,
            IEmailService emailService)
        {
            _dashboardRepository = dashboardRepository;
            _emailService = emailService;
        }

        [Authorize]
        [HttpPost("dashboard")]
        public async Task<IActionResult> GetDashboardData([FromBody] List<int> subStageIds,
            [FromQuery] DateTime? periodDate,
            [FromQuery] int? stateId,
            [FromQuery] int? projectId,
            [FromQuery] int? areaId,
            [FromQuery] int? phaseId,
            [FromQuery] int? stageId,
            [FromQuery] int? layerId,
            [FromQuery] int? subStageId,
            [FromQuery] int? subSpecialtyId,
            [FromQuery] int? userId
        )
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                if (subStageIds == null || !subStageIds.Any())
                    return BadRequest(new { message = "Debe escoger al menos una subetapa" });

                var lessonsByPhaseTask = _dashboardRepository.GetLessonsByPhase(periodDate, userId);
                var lessonsByProjectTask = _dashboardRepository.GetLessonsByProject(periodDate, userId);
                var lessonsByPhaseStageTask = _dashboardRepository.GetLessonsByPhaseAndStage(periodDate, userId);
                var lessonsBySubStage = _dashboardRepository.GetLessonsBySubStage(subStageIds, periodDate, userId);

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
        public async Task<IActionResult> SendPDF(IFormFile pdf)
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
    }
}
