using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Archivos;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Habilitacion.Presentation
{
    [ApiController]
    [Route("api/v1/habilitacion/archivos")]
    [Authorize]
    public class ArchivoHabilitacionController : ControllerBase
    {
        private readonly ISharePointHabService _sharePoint;
        private readonly ILogger<ArchivoHabilitacionController> _logger;

        public ArchivoHabilitacionController(
            ISharePointHabService sharePoint,
            ILogger<ArchivoHabilitacionController> logger)
        {
            _sharePoint = sharePoint;
            _logger = logger;
        }

        [HttpGet("ver")]
        public async Task<IActionResult> Ver([FromQuery] string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return BadRequest(new { message = "Parámetro 'url' requerido." });

                var path = Uri.UnescapeDataString(url);
                var downloadUrl = await _sharePoint.GetDownloadUrlAsync(path);
                if (string.IsNullOrWhiteSpace(downloadUrl))
                    return NotFound(new { message = "Archivo no encontrado." });

                return Redirect(downloadUrl);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ArchivoHabilitacionController.Ver"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        private static readonly HashSet<string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx"
        };

        private const long MaxFileSize = 50_000_000;

        [HttpPost("subir")]
        [RequestSizeLimit(MaxFileSize)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Subir([FromForm] SubirArchivoRequest request)
        {
            try
            {
                var file = request.File;
                var contexto = request.Contexto;

                if (file is null || file.Length == 0)
                    return BadRequest(new { message = "No se recibió ningún archivo." });

                if (file.Length > MaxFileSize)
                    return BadRequest(new { message = "El archivo excede el tamaño máximo de 50 MB." });

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext) || !ExtensionesPermitidas.Contains(ext))
                    return BadRequest(new { message = "Tipo de archivo no permitido. Use PDF, imágenes o documentos Office." });

                using var stream = file.OpenReadStream();
                var path = await _sharePoint.SubirArchivoAsync(stream, file.FileName, contexto);

                return Ok(new { url = path });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ArchivoHabilitacionController.Subir"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("url")]
        public async Task<IActionResult> GetUrl([FromQuery] string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return BadRequest(new { message = "Parámetro 'path' requerido." });

                var downloadUrl = await _sharePoint.GetDownloadUrlAsync(Uri.UnescapeDataString(path));
                if (string.IsNullOrWhiteSpace(downloadUrl))
                    return NotFound(new { message = "Archivo no encontrado." });

                return Ok(new { url = downloadUrl });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ArchivoHabilitacionController.GetUrl"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("descargar")]
        public async Task<IActionResult> Descargar([FromQuery] string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return BadRequest(new { message = "Parámetro 'url' requerido." });

                var path = Uri.UnescapeDataString(url);
                var downloadUrl = await _sharePoint.GetDownloadUrlAsync(path);
                if (string.IsNullOrWhiteSpace(downloadUrl))
                    return NotFound(new { message = "Archivo no encontrado." });

                Response.Headers["Content-Disposition"] = "attachment";
                return Redirect(downloadUrl);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ArchivoHabilitacionController.Descargar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
