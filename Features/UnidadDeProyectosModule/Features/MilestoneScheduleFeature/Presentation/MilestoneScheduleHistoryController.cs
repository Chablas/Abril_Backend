using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Presentation
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [RequireFeature("mejora-continua.milestone-schedule")]
    public class MilestoneScheduleHistoryController : ControllerBase
    {
        private readonly IMilestoneScheduleHistoryService _service;
        private readonly IEmailService _emailService;
        private readonly ILogger<MilestoneScheduleHistoryController> _logger;

        public MilestoneScheduleHistoryController(
            IMilestoneScheduleHistoryService service,
            IEmailService emailService,
            ILogger<MilestoneScheduleHistoryController> logger)
        {
            _service = service;
            _emailService = emailService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllByProjectIdFactory([FromQuery] int projectId)
        {
            try
            {
                var result = await _service.GetAllByProjectId(projectId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost]
        [RequireFeature("mejora-continua.milestone-schedule.editar")]
        public async Task<IActionResult> Create([FromBody] MilestoneScheduleHistoryCreateDTO dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
                var result = await _service.Create(dto, userId);

                if (result.Changes.Any())
                {
                    // Fire-and-forget: la notificación de cambios no debe bloquear la respuesta al
                    // usuario. Antes se hacía `await` acá mismo — si el proveedor de correo (SMTP/
                    // SendGrid/PowerAutomate) no respondía, la petición entera (y "Guardar cronograma"
                    // en el frontend) se quedaba colgada indefinidamente.
                    var body = BuildEmailBody(result);
                    _ = EnviarNotificacionCambiosAsync(body);
                }

                return Ok(new { message = "Cronograma creado exitosamente" });
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

        private async Task EnviarNotificacionCambiosAsync(string body)
        {
            try
            {
                await _emailService.SendAsync(
                    to: new List<string> { "calvarez@abril.pe", "alvarezvillegaschristian@outlook.com" },
                    subject: "Cambios en el cronograma",
                    body: body,
                    isHtml: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo enviar la notificación de cambios de cronograma.");
            }
        }

        private string BuildEmailBody(ScheduleChangeResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Proyecto: {result.ProjectName}<br><br>");
            sb.AppendLine("Se detectaron los siguientes cambios:<br><br>");

            foreach (var change in result.Changes)
            {
                sb.Append($"Hito: {change.MilestoneDescription}: {change.ChangeType}");

                if (change.ChangeType == "Actualizado")
                {
                    var details = new List<string>();
                    if (change.OrderChanged) details.Add("orden");
                    if (change.StartDateChanged) details.Add("fecha inicio");
                    if (change.EndDateChanged) details.Add("fecha fin");
                    sb.Append($" (Cambios en: {string.Join(", ", details)})");
                }

                sb.AppendLine("<br>");
            }

            return sb.ToString();
        }
    }
}
