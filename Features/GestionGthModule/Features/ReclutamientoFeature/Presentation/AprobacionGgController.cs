using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Presentation
{
    /// <summary>
    /// Aprobación de Gerencia General de una solicitud de personal. Dos caras:
    ///   • Pública (Gerente General): GET/POST por token, sin autenticación (<c>[AllowAnonymous]</c>).
    ///     El GG entra desde el correo y no tiene por qué iniciar sesión.
    ///   • Solicitante: reenviar el correo de su propia solicitud. Protegida por JWT.
    /// La clase no lleva <c>[Authorize]</c> a nivel de tipo para permitir los endpoints anónimos.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-gth/aprobacion-gerencia")]
    public class AprobacionGgController : ControllerBase
    {
        private readonly IAprobacionGgService _service;
        private readonly ILogger<AprobacionGgController> _logger;

        public AprobacionGgController(IAprobacionGgService service, ILogger<AprobacionGgController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── Público (Gerencia General, acceso por token) ───────────────────────

        /// <summary>
        /// Solicitud a decidir por token: cabecera + todas sus vacantes, en una sola petición.
        /// Si ya fue decidida, la respuesta lo indica y la página se muestra en modo lectura.
        /// </summary>
        [HttpGet("publico")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublico([FromQuery] string token)
        {
            try
            {
                return Ok(await _service.GetPublico(token));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AprobacionGgController.GetPublico");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Registra la decisión de Gerencia General (aprobar todas, algunas o rechazar todas). Las
        /// vacantes aprobadas pasan a VALIDACION_GTH y se notifican a GTH; las rechazadas quedan en
        /// RECHAZADO_GG. Se puede decidir una sola vez.
        /// </summary>
        [HttpPost("publico/decision")]
        [AllowAnonymous]
        public async Task<IActionResult> RegistrarDecision([FromQuery] string token, [FromBody] AprobacionGgDecisionDto dto)
        {
            try
            {
                return Ok(await _service.RegistrarDecision(token, dto));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AprobacionGgController.RegistrarDecision");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        // ── Solicitante ────────────────────────────────────────────────────────

        /// <summary>
        /// Reenvía el correo de aprobación a Gerencia General (para cuando el primer envío falló o
        /// se corrigieron los destinatarios). Solo el solicitante dueño de la solicitud, y solo
        /// mientras el GG no haya decidido. El enlace no cambia.
        /// </summary>
        [HttpPost("requerimiento/{id:int}/reenviar")]
        [Authorize]
        public async Task<IActionResult> Reenviar(int id)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (int?)null;
                return Ok(await _service.Reenviar(id, userId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AprobacionGgController.Reenviar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
