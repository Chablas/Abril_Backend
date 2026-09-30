using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Constants;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Abril_Backend.Shared.Services.Convivir.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Presentation
{
    /// <summary>
    /// Modal «Documentos» de la pantalla Propietarios: lo que el propietario ve en «Mis documentos»
    /// de la app. Misma feature que la pantalla (por ahora la tiene ADMINISTRADOR DEL SISTEMA).
    /// </summary>
    [ApiController]
    [Authorize]
    [RequireFeature(PropietariosFeatures.Gestion)]
    [Route("api/v1/propietarios/{personId:int}/documentos")]
    public class PropietarioDocumentosController : ControllerBase
    {
        private const string ErrorServidor = "Error del servidor. Por favor contactar al administrador del sistema.";

        private readonly IPropietarioDocumentosService _service;
        private readonly ILogger<PropietarioDocumentosController> _logger;

        public PropietarioDocumentosController(IPropietarioDocumentosService service, ILogger<PropietarioDocumentosController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int? UsuarioActual() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        /// <summary>Tipos + propiedades con sus documentos.</summary>
        [HttpGet]
        public async Task<IActionResult> GetDocumentos(int personId)
        {
            try
            {
                return Ok(await _service.GetDocumentos(personId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PropietarioDocumentosController.GetDocumentos");
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        /// <summary>
        /// Guardar del modal. Multipart: <c>data</c> = JSON con la lista de documentos nuevos
        /// (propietarioId, tipoId, nombre); <c>archivos</c> = sus archivos, en el mismo orden.
        /// Devuelve el modal repintado.
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(PropietarioDocumentoStorage.MaxRequestBytes)]
        public async Task<IActionResult> Guardar(int personId, [FromForm] string? data, [FromForm] List<IFormFile>? archivos)
        {
            try
            {
                var userId = UsuarioActual();
                if (userId == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                return Ok(await _service.Guardar(personId, data, archivos, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PropietarioDocumentosController.Guardar");
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        /// <summary>Baja lógica. Devuelve el modal repintado.</summary>
        [HttpDelete("{documentoId:int}")]
        public async Task<IActionResult> Eliminar(int personId, int documentoId)
        {
            try
            {
                var userId = UsuarioActual();
                if (userId == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                return Ok(await _service.Eliminar(personId, documentoId, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PropietarioDocumentosController.Eliminar");
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        /// <summary>El archivo, con el nombre con que se subió. No lo marca como leído: eso es del propietario.</summary>
        [HttpGet("{documentoId:int}/archivo")]
        public async Task<IActionResult> Descargar(int personId, int documentoId)
        {
            try
            {
                var (contenido, contentType, nombreArchivo) = await _service.Descargar(personId, documentoId);
                return File(contenido, contentType, nombreArchivo);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PropietarioDocumentosController.Descargar");
                return StatusCode(500, new { message = ErrorServidor });
            }
        }
    }
}
