using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SolicitudSalidaCreateDto dto)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                    ? id : (int?)null;
                var newId = await _service.Create(dto, userId);
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
