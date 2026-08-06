using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Configuracion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    /// <summary>
    /// Configuración de EMOs. Acceso restringido por la feature
    /// "ssoma.salud-ocupacional.emos.configuracion". Por ahora expone una única
    /// funcionalidad: CRUD de los destinatarios (principales y copias) de los
    /// correos de programación de EMO — los que se envían al programar un EMO a
    /// mano desde /emos y los del cron diario de programación automática.
    /// </summary>
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/emos/configuracion")]
    [Authorize]
    [RequireFeature("ssoma.salud-ocupacional.emos.configuracion")]
    public class EmoConfiguracionController : ControllerBase
    {
        private readonly IEmoCorreoConfigService _service;
        private readonly ILogger<EmoConfiguracionController> _logger;

        public EmoConfiguracionController(
            IEmoCorreoConfigService service,
            ILogger<EmoConfiguracionController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>Principales y copias en una sola petición.</summary>
        [HttpGet("correos")]
        public async Task<IActionResult> GetCorreos()
        {
            try { return Ok(await _service.GetConfig()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoConfiguracionController.GetCorreos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("correos")]
        public async Task<IActionResult> CrearCorreo([FromBody] EmoCorreoDestinatarioCreateDto dto)
        {
            try
            {
                var id = await _service.Create(dto);
                return Ok(new { id, message = "Destinatario agregado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoConfiguracionController.CrearCorreo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("correos/{id:int}")]
        public async Task<IActionResult> ActualizarCorreo(int id, [FromBody] EmoCorreoDestinatarioUpdateDto dto)
        {
            try
            {
                await _service.Update(id, dto);
                return Ok(new { message = "Destinatario actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoConfiguracionController.ActualizarCorreo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("correos/{id:int}/active")]
        public async Task<IActionResult> ActualizarActivo(int id, [FromBody] EmoCorreoActiveDto dto)
        {
            try
            {
                await _service.SetActive(id, dto.Active);
                return Ok(new { message = "Configuración de correo actualizada." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoConfiguracionController.ActualizarActivo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("correos/{id:int}")]
        public async Task<IActionResult> EliminarCorreo(int id)
        {
            try
            {
                await _service.Delete(id);
                return Ok(new { message = "Destinatario eliminado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoConfiguracionController.EliminarCorreo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
