using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Archivos;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Habilitacion.Presentation
{
    [ApiController]
    [Route("api/v1/habilitacion/archivos")]
    [Authorize]
    public class ArchivoHabilitacionController : ControllerBase
    {
        private readonly ISharePointHabService _sharePoint;
        private readonly IHabTrabajadorRepository _habTrabajadorRepo;
        private readonly ILogger<ArchivoHabilitacionController> _logger;

        public ArchivoHabilitacionController(
            ISharePointHabService sharePoint,
            IHabTrabajadorRepository habTrabajadorRepo,
            ILogger<ArchivoHabilitacionController> logger)
        {
            _sharePoint = sharePoint;
            _habTrabajadorRepo = habTrabajadorRepo;
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
            ".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx", ".zip"
        };

        private const long MaxFileSize = 3_000_000_000;

        [HttpPost("subir")]
        [RequestSizeLimit(MaxFileSize)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Subir([FromForm] SubirArchivoRequest request)
        {
            try
            {
                var file = request.File;
                var contexto = request.Contexto;
                _logger.LogInformation("Subir: FileName={FileName}, Length={Length}, ContentType={ContentType}, HabTrabajadorId={HabTrabajadorId}",
                    file?.FileName, file?.Length, file?.ContentType, request.HabTrabajadorId);

                if (file is null || file.Length == 0)
                {
                    _logger.LogWarning("Subir 400: archivo nulo o vacío");
                    return BadRequest(new { message = "No se recibió ningún archivo." });
                }

                if (file.Length > MaxFileSize)
                {
                    _logger.LogWarning("Subir 400: tamaño {Length} supera límite", file.Length);
                    return BadRequest(new { message = "El archivo excede el tamaño máximo de 50 MB." });
                }

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext) || !ExtensionesPermitidas.Contains(ext))
                {
                    _logger.LogWarning("Subir 400: extensión no permitida '{Ext}'", ext);
                    return BadRequest(new { message = "Tipo de archivo no permitido. Use PDF, imágenes o documentos Office." });
                }

                using var stream = file.OpenReadStream();
                var path = await _sharePoint.SubirArchivoAsync(stream, file.FileName, contexto);
                var realUrl = await _sharePoint.GetDownloadUrlAsync(path);

                if (request.HabTrabajadorId is int habId)
                {
                    var esContratista = User.FindFirst("tipo")?.Value == "CONTRATISTA";

                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    int? userId = userIdClaim != null && int.TryParse(userIdClaim.Value, out var uid) ? uid : null;

                    int? empresaIdClaim = null;
                    if (esContratista)
                    {
                        var ec = User.FindFirst("empresaId")?.Value;
                        if (int.TryParse(ec, out var eid)) empresaIdClaim = eid;
                        userId = null;
                    }

                    var updateDto = new WorkerEntregableUpdateDto
                    {
                        Estado = "Enviado",
                        ArchivoUrl = path,
                        ObsContratista = request.ObsContratista
                    };

                    var entregable = await _habTrabajadorRepo.UpdateEntregableAsync(habId, updateDto, userId, empresaIdClaim);

                    return Ok(new
                    {
                        path,
                        url = realUrl,
                        habTrabajadorId = entregable.Id,
                        estado = entregable.Estado
                    });
                }

                return Ok(new { path, url = realUrl });
            }
            catch (AbrilException ex) { _logger.LogError(ex, "Error en Subir (AbrilException {StatusCode})", ex.StatusCode); return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en Subir"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
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
        [AllowAnonymous]
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
