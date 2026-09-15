using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionGthModule.Features.OnboardingFeature.Presentation
{
    /// <summary>
    /// Onboarding de nuevos colaboradores: la fase que sigue a Reclutamiento. Solo entran acá los
    /// candidatos cuyo requerimiento quedó cerrado, o sea los que ya firmaron su carta oferta y GTH
    /// se la aprobó.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-gth/onboarding")]
    [Authorize]
    public class OnboardingController : ControllerBase
    {
        private readonly IOnboardingService _service;
        private readonly ILogger<OnboardingController> _logger;

        public OnboardingController(IOnboardingService service, ILogger<OnboardingController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>
        /// Todo lo de la pantalla en una sola petición: tarjetas de resumen, embudo de fases y tabla
        /// de colaboradores ingresados. Los que terminaron reclutamiento entran solos a esa tabla.
        /// </summary>
        /// <remarks>Acceso por feature: los roles con <c>gestion-gth.onboarding</c> en role_feature.</remarks>
        [HttpGet("bandeja")]
        [RequireFeature("gestion-gth.onboarding")]
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
                _logger.LogError(ex, "Error en OnboardingController.GetBandeja");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Envía el aviso al responsable de obra (fase «Correo de bienvenida»): el correo que le
        /// pide al coordinador administrativo del proyecto que prevea espacio y condiciones para el
        /// que entra. Solo aplica a los ingresos a una obra o sede.
        /// </summary>
        /// <remarks>Acceso por feature: los roles con <c>gestion-gth.onboarding</c> en role_feature.</remarks>
        [HttpPost("{id:int}/aviso-obra")]
        [RequireFeature("gestion-gth.onboarding")]
        public async Task<IActionResult> EnviarAvisoObra(int id)
        {
            try
            {
                return Ok(await _service.EnviarAvisoObra(id, UserId()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en OnboardingController.EnviarAvisoObra");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Avanza el onboarding a la fase siguiente del checklist. Cada fase valida lo que exige para
        /// poder cerrarse; hoy ninguna está implementada todavía.
        /// </summary>
        /// <remarks>Acceso por feature: los roles con <c>gestion-gth.onboarding</c> en role_feature.</remarks>
        [HttpPost("{id:int}/avanzar")]
        [RequireFeature("gestion-gth.onboarding")]
        public async Task<IActionResult> Avanzar(int id)
        {
            try
            {
                return Ok(await _service.Avanzar(id, UserId()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en OnboardingController.Avanzar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Id del usuario autenticado (null si el token no lo trae).</summary>
        private int? UserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
    }
}
