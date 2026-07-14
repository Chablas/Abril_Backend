using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Presentation
{
    [ApiController]
    [Route("api/v1/gestion-administrativa/solicitud-salidas")]
    [Authorize]
    public class SolicitudSalidaController : ControllerBase
    {
        private readonly ISolicitudSalidaService _service;
        private readonly ILogger<SolicitudSalidaController> _logger;

        public SolicitudSalidaController(ISolicitudSalidaService service, ILogger<SolicitudSalidaController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetMySolicitudes(
            [FromQuery] int? lugarProyectoId,
            [FromQuery] string? estadoAprobacion,
            [FromQuery] string? estadoRendicion)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                    ? id : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                var filters = new SolicitudSalidaFiltersDto
                {
                    LugarProyectoId  = lugarProyectoId,
                    EstadoAprobacion = estadoAprobacion,
                    EstadoRendicion  = estadoRendicion,
                };
                return Ok(await _service.GetByUserId(userId.Value, filters));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudSalidaController.GetMySolicitudes");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("filter-data")]
        public async Task<IActionResult> GetFilterData()
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                    ? id : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.GetFilterData(userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudSalidaController.GetFilterData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("form-data")]
        public async Task<IActionResult> GetFormData()
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                    ? id : (int?)null;
                return Ok(await _service.GetFormData(userId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudSalidaController.GetFormData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Crea la solicitud. Multipart: <c>data</c> = JSON del SolicitudSalidaCreateDto;
        /// <c>adjuntos</c> + <c>adjuntosTrayectoIndex</c> = documentos adjuntos por índice de
        /// trayecto (0-based), obligatorios cuando el motivo tiene requiere_adjunto = true.
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB total
        public async Task<IActionResult> Create(
            [FromForm] string data,
            [FromForm] List<IFormFile>? adjuntos,
            [FromForm] List<string>? adjuntosTrayectoIndex)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                    ? id : (int?)null;

                SolicitudSalidaCreateDto? dto;
                try
                {
                    // Web defaults = mismas convenciones (camelCase, TimeOnly "HH:mm") que el body JSON anterior.
                    dto = JsonSerializer.Deserialize<SolicitudSalidaCreateDto>(
                        data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                }
                catch (JsonException)
                {
                    return BadRequest(new { message = "El campo 'data' no es un JSON válido." });
                }
                if (dto == null)
                    return BadRequest(new { message = "Datos de la solicitud no recibidos." });

                var files = adjuntos ?? new List<IFormFile>();
                var indices = adjuntosTrayectoIndex ?? new List<string>();
                if (files.Count != indices.Count)
                    return BadRequest(new { message = "La cantidad de adjuntos y de índices de trayecto debe coincidir." });

                var pares = new List<(int TrayectoIndex, IFormFile File)>(files.Count);
                for (int i = 0; i < files.Count; i++)
                {
                    if (!int.TryParse(indices[i], out var trayectoIndex))
                        return BadRequest(new { message = $"Índice de trayecto inválido en la posición {i + 1}: '{indices[i]}'." });
                    pares.Add((trayectoIndex, files[i]));
                }

                var newId = await _service.Create(dto, userId, pares);
                return Ok(new { id = newId, message = "Solicitud de salida registrada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudSalidaController.Create");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("{id:int}/detalle")]
        public async Task<IActionResult> GetDetalle(int id)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                return Ok(await _service.GetDetalle(id, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudSalidaController.GetDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("trayectos/{trayectoId:int}/capturas")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB total
        public async Task<IActionResult> UploadCapturasTrayecto(
            int trayectoId,
            [FromForm] List<IFormFile> files,
            [FromForm] List<string> montos)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                if (files == null || montos == null || files.Count != montos.Count)
                    return BadRequest(new { message = "La cantidad de archivos y montos debe coincidir." });

                var items = new List<(IFormFile File, decimal Monto)>(files.Count);
                for (int i = 0; i < files.Count; i++)
                {
                    if (!decimal.TryParse(montos[i], System.Globalization.NumberStyles.Number,
                                          System.Globalization.CultureInfo.InvariantCulture, out var monto))
                        return BadRequest(new { message = $"Monto inválido en la posición {i + 1}: '{montos[i]}'." });
                    items.Add((files[i], monto));
                }

                var creadas = await _service.UploadCapturasToTrayecto(trayectoId, items, userId.Value);
                return Ok(creadas);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudSalidaController.UploadCapturasTrayecto");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        // ── Endpoints públicos invocados desde los links del email ──────────

        [HttpGet("aprobar")]
        [AllowAnonymous]
        public async Task<IActionResult> AprobarDesdeEmail([FromQuery] string token)
        {
            var html = await _service.ProcessAprobarFromEmail(token);
            return Content(html, "text/html; charset=utf-8");
        }

        [HttpGet("rechazar")]
        [AllowAnonymous]
        public IActionResult RechazarFormDesdeEmail([FromQuery] string token)
        {
            var html = _service.RenderRechazarForm(token);
            return Content(html, "text/html; charset=utf-8");
        }

        [HttpPost("rechazar")]
        [AllowAnonymous]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> RechazarDesdeEmail([FromForm] string token, [FromForm] string? motivoRechazo)
        {
            var html = await _service.ProcessRechazarFromEmail(token, motivoRechazo);
            return Content(html, "text/html; charset=utf-8");
        }
    }
}
