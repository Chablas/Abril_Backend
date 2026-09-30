using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.ConvivirModule.Features.DocumentosFeature.Presentation
{
    /// <summary>
    /// «Mis documentos» de la app. Mismo acceso que Inicio: solo el rol PROPIETARIO, y cada uno
    /// solo los de sus propiedades (el repositorio filtra por el usuario del token).
    /// </summary>
    [ApiController]
    [Authorize(Roles = Roles.Propietario)]
    [Route("api/v1/convivir/documentos")]
    public class ConvivirDocumentosController : ControllerBase
    {
        private readonly IConvivirDocumentosService _service;
        private readonly ILogger<ConvivirDocumentosController> _logger;

        public ConvivirDocumentosController(IConvivirDocumentosService service, ILogger<ConvivirDocumentosController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetDocumentos([FromQuery] int? propietarioId = null)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                    return Unauthorized(new { message = "Tu sesión venció. Vuelve a ingresar." });

                return Ok(await _service.GetDocumentos(userId, propietarioId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConvivirDocumentosController.GetDocumentos");
                return StatusCode(500, new { message = "No pudimos cargar tus documentos. Inténtalo de nuevo en unos minutos." });
            }
        }

        /// <summary>El archivo, con el nombre con que se subió. La primera descarga lo marca como leído.</summary>
        [HttpGet("{documentoId:int}/archivo")]
        public async Task<IActionResult> Descargar(int documentoId)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                    return Unauthorized(new { message = "Tu sesión venció. Vuelve a ingresar." });

                var (contenido, contentType, nombreArchivo) = await _service.Descargar(userId, documentoId);
                return File(contenido, contentType, nombreArchivo);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConvivirDocumentosController.Descargar");
                return StatusCode(500, new { message = "No pudimos abrir el documento. Inténtalo de nuevo en unos minutos." });
            }
        }
    }
}
