using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Presentation
{
    [ApiController]
    [Route("api/v1/gestion-gth/reclutamiento")]
    [Authorize]
    public class ReclutamientoController : ControllerBase
    {
        private readonly IReclutamientoService _service;
        private readonly ILogger<ReclutamientoController> _logger;

        public ReclutamientoController(IReclutamientoService service, ILogger<ReclutamientoController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>Área del solicitante + catálogos (puestos, tipos, proyectos) del formulario, en una sola petición.</summary>
        [HttpGet("form-data")]
        public async Task<IActionResult> GetFormData()
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;
                return Ok(await _service.GetFormData(userId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReclutamientoController.GetFormData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Destinatarios configurados del correo de nueva solicitud (principales + copias).</summary>
        /// <remarks>Acceso dinámico por feature: los roles con <c>gestion-gth.reclutamiento.configuracion</c> en role_feature.</remarks>
        [HttpGet("config/correo-destinatarios")]
        [RequireFeature("gestion-gth.reclutamiento.configuracion")]
        public async Task<IActionResult> GetCorreoDestinatarios()
        {
            try
            {
                return Ok(await _service.GetCorreoDestinatarios());
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReclutamientoController.GetCorreoDestinatarios");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Guarda los destinatarios del correo de nueva solicitud (reemplaza ambas listas).</summary>
        /// <remarks>Acceso dinámico por feature: los roles con <c>gestion-gth.reclutamiento.configuracion</c> en role_feature.</remarks>
        [HttpPut("config/correo-destinatarios")]
        [RequireFeature("gestion-gth.reclutamiento.configuracion")]
        public async Task<IActionResult> SaveCorreoDestinatarios([FromBody] CorreoDestinatariosDto dto)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;
                await _service.SaveCorreoDestinatarios(dto, userId);
                return Ok(new { message = "Destinatarios del correo actualizados." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReclutamientoController.SaveCorreoDestinatarios");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Tabla "Mis solicitudes de vacante": requerimientos registrados por el usuario.</summary>
        [HttpGet("mis-solicitudes")]
        public async Task<IActionResult> GetMisSolicitudes()
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;
                return Ok(await _service.GetMisSolicitudes(userId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReclutamientoController.GetMisSolicitudes");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Detalle de seguimiento de un requerimiento (cabecera + fases del pipeline), en una sola petición.</summary>
        [HttpGet("requerimiento/{id:int}/seguimiento")]
        public async Task<IActionResult> GetSeguimiento(int id)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (int?)null;
                return Ok(await _service.GetSeguimiento(id, userId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReclutamientoController.GetSeguimiento");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Crea la solicitud de personal. Multipart: <c>data</c> = JSON de
        /// <see cref="SolicitudPersonalCreateDto"/>; <c>sustento</c> = adjunto opcional (PDF/DOC/DOCX/XLS/XLSX).
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(15 * 1024 * 1024)] // 10 MB de sustento + margen
        public async Task<IActionResult> Create([FromForm] string data, [FromForm] IFormFile? sustento)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;

                SolicitudPersonalCreateDto? dto;
                try
                {
                    dto = JsonSerializer.Deserialize<SolicitudPersonalCreateDto>(
                        data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                catch (JsonException)
                {
                    return BadRequest(new { message = "El campo 'data' no es un JSON válido." });
                }
                if (dto == null)
                    return BadRequest(new { message = "Datos de la solicitud no recibidos." });

                var result = await _service.Create(dto, userId, sustento);
                var msg = result.Codigos.Count == 1
                    ? $"Solicitud registrada. Requerimiento generado: {result.Codigos[0]}."
                    : $"Solicitud registrada. Se generaron {result.Codigos.Count} requerimientos: {string.Join(", ", result.Codigos)}.";
                return Ok(new { id = result.SolicitudId, codigos = result.Codigos, message = msg });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReclutamientoController.Create");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
