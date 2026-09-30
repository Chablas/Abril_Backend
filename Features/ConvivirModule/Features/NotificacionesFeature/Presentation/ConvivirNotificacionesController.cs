using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.ConvivirModule.Features.NotificacionesFeature.Presentation
{
    /// <summary>
    /// La campana de la app. Mismo acceso que Inicio: solo el rol PROPIETARIO, y cada uno solo sus
    /// avisos (el repositorio filtra por el usuario del token).
    /// </summary>
    [ApiController]
    [Authorize(Roles = Roles.Propietario)]
    [Route("api/v1/convivir/notificaciones")]
    public class ConvivirNotificacionesController : ControllerBase
    {
        private readonly IConvivirNotificacionesService _service;
        private readonly ILogger<ConvivirNotificacionesController> _logger;

        public ConvivirNotificacionesController(IConvivirNotificacionesService service, ILogger<ConvivirNotificacionesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetNotificaciones()
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                    return Unauthorized(new { message = "Tu sesión venció. Vuelve a ingresar." });

                return Ok(await _service.GetNotificaciones(userId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConvivirNotificacionesController.GetNotificaciones");
                return StatusCode(500, new { message = "No pudimos cargar tus notificaciones. Inténtalo de nuevo en unos minutos." });
            }
        }

        /// <summary>Al abrir un aviso desde la campana. Leerlo dos veces no cambia la primera lectura.</summary>
        [HttpPost("{notificacionId:int}/leer")]
        public async Task<IActionResult> MarcarLeida(int notificacionId)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                    return Unauthorized(new { message = "Tu sesión venció. Vuelve a ingresar." });

                await _service.MarcarLeida(userId, notificacionId);
                return NoContent();
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConvivirNotificacionesController.MarcarLeida");
                return StatusCode(500, new { message = "No pudimos actualizar tus notificaciones. Inténtalo de nuevo en unos minutos." });
            }
        }

        [HttpPost("leer-todas")]
        public async Task<IActionResult> MarcarTodasLeidas()
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                    return Unauthorized(new { message = "Tu sesión venció. Vuelve a ingresar." });

                await _service.MarcarTodasLeidas(userId);
                return NoContent();
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConvivirNotificacionesController.MarcarTodasLeidas");
                return StatusCode(500, new { message = "No pudimos actualizar tus notificaciones. Inténtalo de nuevo en unos minutos." });
            }
        }
    }
}
