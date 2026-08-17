using System.Security.Claims;
using System.Text.Json;
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
    /// candidatos que el área solicitante seleccionó y cuyo requerimiento quedó cerrado.
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
        /// Todo lo de la pantalla en una sola petición: tarjetas de resumen, embudo de fases, tabla de
        /// colaboradores ingresados y los candidatos aptos del modal «Nuevo ingreso».
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
        /// Inicia el onboarding de un colaborador (modal «Nuevo ingreso»). Multipart: <c>data</c> =
        /// JSON de <see cref="OnboardingCreateDto"/>; <c>cartaOferta</c> = el archivo de la carta, que
        /// se le envía por correo y se guarda en su file de SharePoint. El correo destino lo resuelve
        /// el backend desde la base de datos (<c>person.email</c>), no viaja desde el frontend salvo
        /// que GTH lo haya corregido a mano.
        /// </summary>
        /// <remarks>Acceso por feature: los roles con <c>gestion-gth.onboarding</c> en role_feature.</remarks>
        [HttpPost]
        [RequireFeature("gestion-gth.onboarding")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20 * 1024 * 1024)] // 15 MB de carta + margen
        public async Task<IActionResult> Iniciar([FromForm] string data, [FromForm] IFormFile? cartaOferta)
        {
            try
            {
                OnboardingCreateDto? dto;
                try
                {
                    dto = JsonSerializer.Deserialize<OnboardingCreateDto>(
                        data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                catch (JsonException)
                {
                    return BadRequest(new { message = "El campo 'data' no es un JSON válido." });
                }
                if (dto == null)
                    return BadRequest(new { message = "Datos del onboarding no recibidos." });

                if (cartaOferta == null || cartaOferta.Length == 0)
                    return BadRequest(new { message = "Adjunta la carta oferta para poder enviarla." });

                using var ms = new MemoryStream();
                await cartaOferta.CopyToAsync(ms);

                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (int?)null;
                var result = await _service.Iniciar(
                    dto,
                    cartaOferta.FileName,
                    cartaOferta.ContentType ?? "application/octet-stream",
                    ms.ToArray(),
                    userId);

                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en OnboardingController.Iniciar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
