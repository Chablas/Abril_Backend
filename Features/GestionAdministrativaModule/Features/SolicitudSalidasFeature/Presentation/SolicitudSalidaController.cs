using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Presentation
{
    [ApiController]
    [Route("api/v1/gestion-administrativa/solicitud-salidas")]
    [Authorize]
    public class SolicitudSalidaController : ControllerBase
    {
        private readonly ISolicitudSalidaService _service;
        private readonly ILogger<SolicitudSalidaController> _logger;

        public SolicitudSalidaController(ISolicitudSalidaService service, ILogger<SolicitudSalidaController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetMySolicitudes()
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                    ? id : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.GetByUserId(userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudSalidaController.GetMySolicitudes");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("form-data")]
        public async Task<IActionResult> GetFormData()
        {
            try
            {
                return Ok(await _service.GetFormData());
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudSalidaController.GetFormData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SolicitudSalidaCreateDto dto)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                    ? id : (int?)null;
                var newId = await _service.Create(dto, userId);
                return Ok(new { id = newId, message = "Solicitud de salida registrada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudSalidaController.Create");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
