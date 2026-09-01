using System.Security.Claims;
using System.Text.Json;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Presentation
{
    /// <summary>
    /// Carta oferta del seleccionado: el ÚLTIMO paso del proceso de reclutamiento. Se opera desde el
    /// detalle del requerimiento en la bandeja de GTH.
    ///
    /// Va en su propio controller y no dentro de <see cref="ReclutamientoController"/> porque es un
    /// flujo con su propio ciclo (envío → firma → aprobación) y su propia contraparte pública
    /// (<see cref="CartaOfertaFirmaController"/>), pero comparte el prefijo de ruta para que la API
    /// del requerimiento se lea de corrido.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-gth/reclutamiento/requerimiento/{id:int}/carta-oferta")]
    [Authorize]
    public class CartaOfertaController : ControllerBase
    {
        private readonly ICartaOfertaService _service;
        private readonly ILogger<CartaOfertaController> _logger;

        public CartaOfertaController(ICartaOfertaService service, ILogger<CartaOfertaController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>
        /// Envía la carta oferta al seleccionado. Multipart: <c>data</c> = JSON de
        /// <see cref="CartaOfertaEnviarDto"/>; <c>carta</c> = el archivo (PDF), que se guarda en el
        /// file del colaborador en SharePoint. Al candidato se le envía un correo con el <b>enlace</b>
        /// para leerla y firmarla en línea, no la carta adjunta. El correo destino lo resuelve el
        /// backend desde la base de datos (<c>person.email</c>) salvo que GTH lo haya corregido a
        /// mano. Solo se acepta con el requerimiento en EMO_APTO o EMO_APTO_RESTRICCIONES.
        /// </summary>
        /// <remarks>Acceso por feature: los roles con <c>gestion-gth.reclutamiento</c> en role_feature.</remarks>
        [HttpPost]
        [RequireFeature("gestion-gth.reclutamiento")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20 * 1024 * 1024)] // 15 MB de carta + margen
        public async Task<IActionResult> Enviar(int id, [FromForm] string data, [FromForm] IFormFile? carta)
        {
            try
            {
                CartaOfertaEnviarDto? dto;
                try
                {
                    dto = JsonSerializer.Deserialize<CartaOfertaEnviarDto>(
                        data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                catch (JsonException)
                {
                    return BadRequest(new { message = "El campo 'data' no es un JSON válido." });
                }
                if (dto == null)
                    return BadRequest(new { message = "Datos de la carta oferta no recibidos." });

                if (carta == null || carta.Length == 0)
                    return BadRequest(new { message = "Adjunta la carta oferta para poder enviarla." });

                using var ms = new MemoryStream();
                await carta.CopyToAsync(ms);

                return Ok(await _service.Enviar(
                    id, dto, carta.FileName, carta.ContentType ?? "application/octet-stream", ms.ToArray(), UserId()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CartaOfertaController.Enviar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Reenvía al candidato el correo con el enlace para ver y firmar su carta oferta. Sirve
        /// cuando el correo del envío no salió, cuando lo perdió o cuando cambió de correo. El token
        /// del enlace original se conserva.
        /// </summary>
        /// <remarks>Acceso por feature: los roles con <c>gestion-gth.reclutamiento</c> en role_feature.</remarks>
        [HttpPost("reenviar")]
        [RequireFeature("gestion-gth.reclutamiento")]
        public async Task<IActionResult> ReenviarEnlace(int id, [FromBody] CartaOfertaReenviarDto? dto)
        {
            try
            {
                return Ok(await _service.ReenviarEnlace(id, dto?.Correo, UserId()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CartaOfertaController.ReenviarEnlace");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Adjunta la carta oferta que el candidato devolvió firmada. Es la vía de RESPALDO: lo normal
        /// es que la firme él mismo desde el enlace público. Se guarda en la carpeta «Carta Oferta
        /// Firmada» del file digital del colaborador y queda pendiente de aprobación por GTH.
        /// </summary>
        /// <remarks>Acceso por feature: los roles con <c>gestion-gth.reclutamiento</c> en role_feature.</remarks>
        [HttpPost("firmada")]
        [RequireFeature("gestion-gth.reclutamiento")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(20 * 1024 * 1024)] // 15 MB de carta + margen
        public async Task<IActionResult> SubirFirmada(int id, [FromForm] IFormFile? archivo)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                    return BadRequest(new { message = "Adjunta la carta oferta firmada." });

                using var ms = new MemoryStream();
                await archivo.CopyToAsync(ms);

                return Ok(await _service.SubirFirmada(
                    id, archivo.FileName, archivo.ContentType ?? "application/octet-stream", ms.ToArray(), UserId()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CartaOfertaController.SubirFirmada");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Aprueba la carta oferta firmada y CIERRA el proceso: el requerimiento pasa a CERRADO y el
        /// seleccionado aparece en Onboarding como candidato por ingresar. Es el único camino al
        /// cierre.
        /// </summary>
        /// <remarks>Acceso por feature: los roles con <c>gestion-gth.reclutamiento</c> en role_feature.</remarks>
        [HttpPost("firmada/aprobar")]
        [RequireFeature("gestion-gth.reclutamiento")]
        public async Task<IActionResult> Aprobar(int id)
        {
            try
            {
                return Ok(await _service.Aprobar(id, UserId()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CartaOfertaController.Aprobar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Id del usuario autenticado (null si el token no lo trae).</summary>
        private int? UserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
    }
}
