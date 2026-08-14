using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Presentation
{
    /// <summary>
    /// Aprobación de Gerencia General de una solicitud de personal. Dos caras, ambas autenticadas:
    ///   • Gerencia: pantalla «Aprobaciones» (bandeja + historial), detalle y decisión.
    ///   • Solicitante: reenviar el correo de su propia solicitud.
    /// Los antiguos endpoints públicos por token ya no existen: el gerente entra desde el correo a
    /// la pantalla y, si no tiene sesión, el login lo devuelve a esa misma URL.
    /// </summary>
    [ApiController]
    [Authorize]
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

        /// <summary>Id del usuario autenticado (claim NameIdentifier del JWT interno).</summary>
        private int? UserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;

        // ── Pantalla «Aprobaciones» (Gerencia) ─────────────────────────────────

        /// <summary>
        /// Pantalla completa en una sola petición: tarjetas de resumen + las solicitudes pendientes
        /// de decidir y el historial de las ya decididas.
        /// </summary>
        [HttpGet("bandeja")]
        public async Task<IActionResult> GetBandeja()
        {
            try
            {
                return Ok(await _service.GetBandeja());
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AprobacionGgController.GetBandeja");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Detalle de una solicitud a decidir: cabecera + todas sus vacantes. Si ya fue decidida,
        /// la respuesta lo indica y el modal se muestra en modo lectura (historial).
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetDetalle(int id)
        {
            try
            {
                return Ok(await _service.GetDetalle(id));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AprobacionGgController.GetDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Registra la decisión de Gerencia General (aprobar todas, algunas o rechazar todas). Las
        /// vacantes aprobadas pasan a VALIDACION_GTH y se notifican a GTH; las rechazadas quedan en
        /// RECHAZADO_GG. Se puede decidir una sola vez.
        /// </summary>
        [HttpPost("{id:int}/decision")]
        public async Task<IActionResult> RegistrarDecision(int id, [FromBody] AprobacionGgDecisionDto dto)
        {
            try
            {
                return Ok(await _service.RegistrarDecision(id, dto, UserId));
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
        /// mientras Gerencia no haya decidido. El enlace no cambia.
        /// </summary>
        [HttpPost("requerimiento/{id:int}/reenviar")]
        public async Task<IActionResult> Reenviar(int id)
        {
            try
            {
                return Ok(await _service.Reenviar(id, UserId));
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
