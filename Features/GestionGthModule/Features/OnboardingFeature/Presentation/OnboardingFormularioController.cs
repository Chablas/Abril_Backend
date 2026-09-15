using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Presentation
{
    /// <summary>
    /// Formulario «Nuevos Talentos» del colaborador que entra. Dos caras:
    ///   • Pública (el colaborador): GET/POST por token, sin autenticación.
    ///   • GTH (bandeja de onboarding): mandar el correo de bienvenida, que es lo que abre ese
    ///     formulario y le da el enlace.
    /// La clase no lleva <c>[Authorize]</c> de tipo para permitir los endpoints anónimos; el de GTH
    /// declara el suyo.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-gth/onboarding/formulario")]
    public class OnboardingFormularioController : ControllerBase
    {
        private readonly IOnboardingFormularioService _service;
        private readonly ILogger<OnboardingFormularioController> _logger;

        public OnboardingFormularioController(
            IOnboardingFormularioService service, ILogger<OnboardingFormularioController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        // ── Público (el colaborador, acceso por token) ────────────────────────

        /// <summary>Formulario público por token: contexto, catálogos y respuestas guardadas.</summary>
        [HttpGet("publico")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublico([FromQuery] string token)
        {
            try { return Ok(await _service.GetPublico(token)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en OnboardingFormularioController.GetPublico");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Recibe el envío del colaborador y marca el formulario como COMPLETADO.</summary>
        [HttpPost("publico")]
        [AllowAnonymous]
        public async Task<IActionResult> GuardarPublico(
            [FromQuery] string token, [FromBody] OnboardingFormularioRespuestasDto respuestas)
        {
            try
            {
                await _service.GuardarPublico(token, respuestas);
                return Ok(new { message = "¡Gracias! Tu formulario fue enviado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en OnboardingFormularioController.GuardarPublico");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        // ── GTH (correo de bienvenida) ────────────────────────────────────────

        /// <summary>
        /// Envía el correo de bienvenida: le abre al colaborador su formulario y le manda el
        /// enlace, la documentación que tiene que enviar y la fecha límite.
        ///
        /// Multipart: <c>data</c> = JSON de <see cref="EnviarBienvenidaDto"/>; <c>archivos</c> =
        /// los documentos normativos que GTH quiera adjuntar (opcionales, con tope de tamaño).
        /// </summary>
        /// <remarks>Acceso por feature: los roles con <c>gestion-gth.onboarding</c> en role_feature.</remarks>
        [HttpPost("{onboardingId:int}/bienvenida")]
        [Authorize]
        [RequireFeature("gestion-gth.onboarding")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10 * 1024 * 1024)] // el tope real de los adjuntos lo pone el servicio
        public async Task<IActionResult> EnviarBienvenida(
            int onboardingId, [FromForm] string? data, [FromForm] List<IFormFile>? archivos)
        {
            try
            {
                EnviarBienvenidaDto? dto = null;
                if (!string.IsNullOrWhiteSpace(data))
                {
                    try
                    {
                        dto = JsonSerializer.Deserialize<EnviarBienvenidaDto>(
                            data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    }
                    catch (JsonException)
                    {
                        return BadRequest(new { message = "No se pudieron leer los datos del envío." });
                    }
                }

                return Ok(await _service.EnviarBienvenida(onboardingId, dto, archivos, UserId()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en OnboardingFormularioController.EnviarBienvenida");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Id del usuario autenticado (null si el token no lo trae).</summary>
        private int? UserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
    }
}
