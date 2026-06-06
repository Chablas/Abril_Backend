using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Archivos;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Helpers;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<ArchivoHabilitacionController> _logger;

        public ArchivoHabilitacionController(
            ISharePointHabService sharePoint,
            IHabTrabajadorRepository habTrabajadorRepo,
            IDbContextFactory<AppDbContext> factory,
            ILogger<ArchivoHabilitacionController> logger)
        {
            _sharePoint = sharePoint;
            _habTrabajadorRepo = habTrabajadorRepo;
            _factory = factory;
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

                    if (esContratista)
                    {
                        var itemId = await _habTrabajadorRepo.GetEntregableItemIdAsync(habId);
                        if (itemId == HabItemIds.InduccionObra)
                            return BadRequest(new { message = "La Inducción de Obra no puede ser enviada por el contratista. Debe ser aprobada presencialmente." });
                    }

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

        [HttpPost("subir-multiple")]
        [RequestSizeLimit(MaxFileSize)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubirMultiple([FromForm] SubirArchivoRequest request)
        {
            try
            {
                var file = request.File;
                if (file is null || file.Length == 0)
                    return BadRequest(new { message = "No se recibió ningún archivo." });
                if (file.Length > MaxFileSize)
                    return BadRequest(new { message = "El archivo excede el tamaño máximo." });
                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext) || !ExtensionesPermitidas.Contains(ext))
                    return BadRequest(new { message = "Tipo de archivo no permitido." });

                using var stream = file.OpenReadStream();
                var path = await _sharePoint.SubirArchivoAsync(stream, file.FileName, request.Contexto);

                string? zipContenido = null;
                if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var zipStream = file.OpenReadStream();
                        using var zip = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);
                        var entradas = zip.Entries
                            .Select(e => new { nombre = e.FullName, tamaño = e.Length })
                            .ToList();
                        zipContenido = System.Text.Json.JsonSerializer.Serialize(entradas);
                    }
                    catch { /* si falla la extracción, no bloqueamos la subida */ }
                }

                return Ok(new
                {
                    path,
                    nombreArchivo = file.FileName,
                    esZip = ext.Equals(".zip", StringComparison.OrdinalIgnoreCase),
                    zipContenido
                });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en SubirMultiple"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("enviar")]
        public async Task<IActionResult> Enviar([FromBody] EnviarDocumentoRequest request)
        {
            try
            {
                if (request.Archivos is null || request.Archivos.Count == 0)
                    return BadRequest(new { message = "Debes adjuntar al menos un archivo." });

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

                using var ctx = _factory.CreateDbContext();

                int? habTrabajadorId = request.HabTrabajadorId;
                int? habEmpresaId = request.HabEmpresaId;
                int? habEquipoId = request.HabEquipoId;

                int versionActual = await ctx.SsHabDocumentoVersion
                    .CountAsync(v =>
                        (habTrabajadorId.HasValue && v.HabTrabajadorId == habTrabajadorId) ||
                        (habEmpresaId.HasValue && v.HabEmpresaId == habEmpresaId) ||
                        (habEquipoId.HasValue && v.HabEquipoId == habEquipoId));

                var primerArchivo = request.Archivos[0];

                var version = new SsHabDocumentoVersion
                {
                    HabTrabajadorId = habTrabajadorId,
                    HabEmpresaId = habEmpresaId,
                    HabEquipoId = habEquipoId,
                    Version = versionActual + 1,
                    ArchivoUrl = primerArchivo.ArchivoUrl,
                    SubidoPorUserId = userId,
                    SubidoPorEmpresaId = empresaIdClaim,
                    EstadoAlSubir = "Enviado",
                    Enviado = true,
                    FechaEnvio = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                ctx.SsHabDocumentoVersion.Add(version);
                await ctx.SaveChangesAsync();

                for (int i = 0; i < request.Archivos.Count; i++)
                {
                    var a = request.Archivos[i];
                    ctx.SsHabDocumentoArchivo.Add(new SsHabDocumentoArchivo
                    {
                        VersionId = version.Id,
                        ArchivoUrl = a.ArchivoUrl,
                        NombreArchivo = a.NombreArchivo,
                        EsZip = a.EsZip,
                        ZipContenido = a.ZipContenido,
                        Orden = i,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                if (habTrabajadorId.HasValue)
                {
                    var ent = await ctx.SsHabTrabajador.FindAsync(habTrabajadorId.Value);
                    if (ent != null)
                    {
                        ent.Estado = "Enviado";
                        ent.ArchivoUrl = primerArchivo.ArchivoUrl;
                        if (!string.IsNullOrEmpty(request.ObsContratista))
                            ent.ObsContratista = request.ObsContratista;
                        ent.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else if (habEmpresaId.HasValue)
                {
                    var ent = await ctx.SsHabEmpresa.FindAsync(habEmpresaId.Value);
                    if (ent != null)
                    {
                        ent.Estado = "Enviado";
                        ent.ArchivoUrl = primerArchivo.ArchivoUrl;
                        if (!string.IsNullOrEmpty(request.ObsContratista))
                            ent.ObsContratista = request.ObsContratista;
                        if (request.Vigencia.HasValue)
                            ent.Vigencia = HabilitacionDateHelper.AsUtc(request.Vigencia);
                        ent.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else if (habEquipoId.HasValue)
                {
                    var ent = await ctx.SsHabEquipo.FindAsync(habEquipoId.Value);
                    if (ent != null)
                    {
                        ent.Estado = "Enviado";
                        ent.ArchivoUrl = primerArchivo.ArchivoUrl;
                        if (!string.IsNullOrEmpty(request.ObsContratista))
                            ent.ObsContratista = request.ObsContratista;
                        ent.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await ctx.SaveChangesAsync();

                return Ok(new { versionId = version.Id, archivos = request.Archivos.Count });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en Enviar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
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
