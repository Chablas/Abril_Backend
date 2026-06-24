using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Dtos;
using Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.VecinosModule.Features.ControlVencimientosFeature.Presentation
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ControlVencimientosController : ControllerBase
    {
        private readonly IControlVencimientosService _service;
        private readonly ILogger<ControlVencimientosController> _logger;

        public ControlVencimientosController(
            IControlVencimientosService service,
            ILogger<ControlVencimientosController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>Listado de licencias/permisos registrados, ordenados por fecha de vencimiento.</summary>
        [HttpGet]
        public async Task<IActionResult> GetLicencias()
        {
            try
            {
                var result = await _service.GetLicencias();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL VENCIMIENTOS GET: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Crea una licencia/permiso subiendo su archivo y sus fechas de vencimiento/recordatorio.</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateLicencia([FromForm] IFormFile file, [FromForm] VecinoLicenciaCreateDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                int userId = userIdClaim != null ? int.Parse(userIdClaim.Value) : 0;

                var licencia = await _service.CreateLicencia(dto, file, userId);
                return Ok(new { licencia, message = "Licencia registrada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONTROL VENCIMIENTOS CREATE: {msg}", ex.ToString());
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
