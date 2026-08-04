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
    /// Formulario de información del postulante. Dos caras:
    ///   • Pública (postulante): GET/POST por token, sin autenticación (<c>[AllowAnonymous]</c>).
    ///   • GTH (bandeja de reclutamiento): enviar el enlace, revisar y aprobar/rechazar. Protegida por
    ///     JWT + feature <c>gestion-gth.reclutamiento</c>.
    /// La clase no lleva <c>[Authorize]</c> a nivel de tipo para permitir los endpoints anónimos; los
    /// de GTH declaran su propio <c>[Authorize]</c> + <c>[RequireFeature]</c>.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-gth/postulante-formulario")]
    public class PostulanteFormularioController : ControllerBase
    {
        private readonly IPostulanteFormularioService _service;
        private readonly ILogger<PostulanteFormularioController> _logger;

        public PostulanteFormularioController(
            IPostulanteFormularioService service,
            ILogger<PostulanteFormularioController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;

        // ── Público (postulante, acceso por token) ────────────────────────────

        /// <summary>Formulario público por token: contexto del proceso + catálogos + respuestas guardadas + estado.</summary>
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
                _logger.LogError(ex, "Error en PostulanteFormularioController.GetPublico");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Recibe el envío del postulante (por token) y marca el formulario como COMPLETADO.</summary>
        [HttpPost("publico")]
        [AllowAnonymous]
        public async Task<IActionResult> GuardarPublico([FromQuery] string token, [FromBody] PostulanteFormularioRespuestasDto respuestas)
        {
            try
            {
                await _service.GuardarPublico(token, respuestas);
                return Ok(new { message = "¡Gracias! Tu formulario fue enviado correctamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PostulanteFormularioController.GuardarPublico");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        // ── GTH (enviar / revisar / decidir) ──────────────────────────────────

        /// <summary>GTH: envía (o reenvía) el formulario al correo del postulante de un candidato aprobado.</summary>
        [HttpPost("candidato/{candidatoId:int}/enviar")]
        [Authorize]
        [RequireFeature("gestion-gth.reclutamiento")]
        public async Task<IActionResult> Enviar(int candidatoId, [FromBody] EnviarFormularioDto dto)
        {
            try
            {
                var result = await _service.Enviar(candidatoId, dto, CurrentUserId);
                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PostulanteFormularioController.Enviar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>GTH: formulario del candidato para revisar (modal "Ver formulario").</summary>
        [HttpGet("candidato/{candidatoId:int}")]
        [Authorize]
        [RequireFeature("gestion-gth.reclutamiento")]
        public async Task<IActionResult> GetRevision(int candidatoId)
        {
            try
            {
                return Ok(await _service.GetRevision(candidatoId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PostulanteFormularioController.GetRevision");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>GTH: registra la decisión sobre el formulario completado (aprobar/rechazar).</summary>
        [HttpPost("candidato/{candidatoId:int}/decision")]
        [Authorize]
        [RequireFeature("gestion-gth.reclutamiento")]
        public async Task<IActionResult> Decision(int candidatoId, [FromBody] FormularioDecisionDto dto)
        {
            try
            {
                var result = await _service.Decision(candidatoId, dto, CurrentUserId);
                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PostulanteFormularioController.Decision");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
