using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Presentation
{
    /// <summary>
    /// Configuración de los correos de Solicitud de Personal. Acceso restringido por la feature
    /// "gestion-gth.reclutamiento.configuracion" (la misma que ya habilitaba el botón
    /// «Configuración» de la pantalla). Expone una sección por correo del flujo, cada una con su
    /// interruptor maestro y sus destinatarios: los dinámicos del catálogo (Gerente General,
    /// gerente del área, área de GTH), que solo se prenden y apagan, y los correos adicionales
    /// escritos a mano, con alta, edición y baja.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-gth/solicitud-personal/configuracion")]
    [Authorize]
    [RequireFeature("gestion-gth.reclutamiento.configuracion")]
    public class CorreoConfiguracionController : ControllerBase
    {
        private readonly ICorreoConfigService _service;
        private readonly ILogger<CorreoConfiguracionController> _logger;

        public CorreoConfiguracionController(
            ICorreoConfigService service,
            ILogger<CorreoConfiguracionController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        private int? UserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        /// <summary>Todos los correos del flujo con sus destinatarios, en una sola petición.</summary>
        [HttpGet("correos")]
        public async Task<IActionResult> GetCorreos()
        {
            try { return Ok(await _service.GetConfig()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreoConfiguracionController.GetCorreos");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Prende o apaga un correo completo (interruptor maestro de la sección).</summary>
        [HttpPut("correos/{tipo}/active")]
        public async Task<IActionResult> SetCorreoActive(string tipo, [FromBody] CorreoActivePatchDto dto)
        {
            try
            {
                await _service.SetCorreoActive(tipo, dto?.Active ?? false, UserId);
                return Ok(new
                {
                    message = (dto?.Active ?? false)
                        ? "El correo volverá a enviarse."
                        : "El correo quedó desactivado: no se enviará.",
                });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreoConfiguracionController.SetCorreoActive");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Alta de un correo adicional dentro de la sección de un correo.</summary>
        [HttpPost("correos/destinatarios")]
        public async Task<IActionResult> CrearAdicional([FromBody] CorreoAdicionalCreateDto dto)
        {
            try
            {
                var id = await _service.CrearAdicional(dto, UserId);
                return Ok(new { id, message = "Destinatario agregado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreoConfiguracionController.CrearAdicional");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Edición de un correo adicional (los dinámicos no se editan).</summary>
        [HttpPut("correos/destinatarios/{id:int}")]
        public async Task<IActionResult> ActualizarAdicional(int id, [FromBody] CorreoAdicionalUpdateDto dto)
        {
            try
            {
                await _service.ActualizarAdicional(id, dto, UserId);
                return Ok(new { message = "Destinatario actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreoConfiguracionController.ActualizarAdicional");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Prende o apaga un destinatario dentro de su correo.</summary>
        [HttpPut("correos/destinatarios/{id:int}/active")]
        public async Task<IActionResult> SetDestinatarioActive(int id, [FromBody] CorreoActivePatchDto dto)
        {
            try
            {
                await _service.SetDestinatarioActive(id, dto?.Active ?? false, UserId);
                return Ok(new { message = "Configuración de correo actualizada." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreoConfiguracionController.SetDestinatarioActive");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Baja de un correo adicional.</summary>
        [HttpDelete("correos/destinatarios/{id:int}")]
        public async Task<IActionResult> EliminarAdicional(int id)
        {
            try
            {
                await _service.EliminarAdicional(id, UserId);
                return Ok(new { message = "Destinatario eliminado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreoConfiguracionController.EliminarAdicional");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
