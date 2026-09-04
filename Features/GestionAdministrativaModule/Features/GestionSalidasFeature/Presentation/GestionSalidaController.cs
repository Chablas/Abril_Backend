using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Presentation
{
    [ApiController]
    [Route("api/v1/gestion-administrativa/gestion-salidas")]
    [Authorize]
    public class GestionSalidaController : ControllerBase
    {
        private readonly IGestionSalidaService _service;
        private readonly ILogger<GestionSalidaController> _logger;

        public GestionSalidaController(IGestionSalidaService service, ILogger<GestionSalidaController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? workerId, [FromQuery] int? lugarProyectoId, [FromQuery] string? estadoRendicion, [FromQuery] string? estadoAprobacion, [FromQuery] string? estadoReembolso = null, [FromQuery] List<int>? areaScopeIds = null, [FromQuery] int page = 1, [FromQuery] string? sortBy = null, [FromQuery] string? sortDir = null, [FromQuery] bool soloHoy = false, [FromQuery] int? rendicionAnio = null, [FromQuery] int? rendicionMes = null)
        {
            try
            {
                var currentUserId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;

                var filters = new GestionSalidaFiltersDto
                {
                    WorkerId            = workerId,
                    LugarProyectoId     = lugarProyectoId,
                    EstadoRendicion     = estadoRendicion,
                    EstadoAprobacion    = estadoAprobacion,
                    EstadoReembolso     = estadoReembolso,
                    FilterAreaScopeIds  = areaScopeIds,
                    SoloHoy             = soloHoy,
                    RendicionAnio       = rendicionAnio,
                    RendicionMes        = rendicionMes,
                    CurrentUserId       = currentUserId,
                    SeesAllOverride     = User.IsInRole(Roles.UsuarioRecepcion),
                    Page                = page < 1 ? 1 : page,
                    SortBy              = sortBy,
                    SortDir             = sortDir,
                };
                return Ok(await _service.GetPaged(filters));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.GetAll");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("exportar-excel")]
        public async Task<IActionResult> ExportarExcel([FromQuery] int? workerId, [FromQuery] int? lugarProyectoId, [FromQuery] string? estadoRendicion, [FromQuery] string? estadoAprobacion, [FromQuery] string? estadoReembolso = null, [FromQuery] List<int>? areaScopeIds = null, [FromQuery] bool soloHoy = false)
        {
            try
            {
                var currentUserId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;

                var filters = new GestionSalidaFiltersDto
                {
                    WorkerId           = workerId,
                    LugarProyectoId    = lugarProyectoId,
                    EstadoRendicion    = estadoRendicion,
                    EstadoAprobacion   = estadoAprobacion,
                    EstadoReembolso    = estadoReembolso,
                    FilterAreaScopeIds = areaScopeIds,
                    SoloHoy            = soloHoy,
                    CurrentUserId      = currentUserId,
                    SeesAllOverride    = User.IsInRole(Roles.UsuarioRecepcion),
                };
                var bytes = await _service.GetExcel(filters);
                return File(
                    bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Gestion_Salidas.xlsx"
                );
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.ExportarExcel");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("{id:int}/detalle")]
        public async Task<IActionResult> GetDetalle(int id)
        {
            try
            {
                var detalle = await _service.GetDetalle(id);
                if (detalle == null)
                    return NotFound(new { message = "Solicitud no encontrada." });
                return Ok(detalle);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.GetDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("filter-data")]
        public async Task<IActionResult> GetFilterData()
        {
            try
            {
                var currentUserId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;

                return Ok(await _service.GetFilterData(currentUserId, User.IsInRole(Roles.UsuarioRecepcion)));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.GetFilterData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("{id:int}/aprobar")]
        public async Task<IActionResult> Aprobar(int id)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                await _service.Aprobar(id, userId.Value);
                return Ok(new { message = "Solicitud aprobada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.Aprobar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("{id:int}/rechazar")]
        public async Task<IActionResult> Rechazar(int id)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                await _service.Rechazar(id, userId.Value);
                return Ok(new { message = "Solicitud rechazada." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.Rechazar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("{id:int}/cancelar")]
        public async Task<IActionResult> Cancelar(int id)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                await _service.Cancelar(id, userId.Value);
                return Ok(new { message = "Solicitud cancelada." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.Cancelar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("{id:int}/hora-salida-real")]
        public async Task<IActionResult> SetHoraSalidaReal(int id, [FromBody] RegistrarHoraSalidaRealDto dto)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                await _service.SetHoraSalidaReal(id, dto.HoraSalidaReal, userId.Value);
                return Ok(new { message = "Hora real registrada." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.SetHoraSalidaReal");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("{id:int}/hora-retorno-real")]
        public async Task<IActionResult> SetHoraRetornoReal(int id, [FromBody] RegistrarHoraRetornoRealDto dto)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                await _service.SetHoraRetornoReal(id, dto.HoraRetornoReal, userId.Value);
                return Ok(new { message = "Hora real registrada." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.SetHoraRetornoReal");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("marcar-rendidas")]
        public async Task<IActionResult> MarcarRendidas([FromBody] MarcarRendidasBulkDto dto)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                if (dto?.Ids == null || dto.Ids.Count == 0)
                    return BadRequest(new { message = "Debes seleccionar al menos una solicitud." });

                var (pdfBytes, count) = await _service.RendirYGenerarPlanilla(dto.Ids, userId.Value);

                // Header custom para que el frontend pueda mostrar el contador en el toast.
                Response.Headers.Append("X-Rendidas-Count", count.ToString());
                Response.Headers.Append("Access-Control-Expose-Headers", "X-Rendidas-Count, Content-Disposition");

                var filename = $"Planilla_Rendicion_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                return File(pdfBytes, "application/pdf", filename);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.MarcarRendidas");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Rinde de una vez TODAS las salidas del mes indicado (sin <c>anio</c>/<c>mes</c>, el
        /// anterior) que estén aptas —aprobadas, no rendidas, con las capturas de todos sus
        /// trayectos y con un motivo reembolsable— dentro del alcance de visibilidad del usuario,
        /// respetando los filtros de trabajador/área/proyecto que vengan en la query. Es lo que la
        /// pantalla ofrece como "seleccionar todas las del mes". Las que no cumplen se ignoran.
        /// </summary>
        [HttpPatch("rendir-mes")]
        public async Task<IActionResult> RendirMes(
            [FromQuery] int? workerId,
            [FromQuery] int? lugarProyectoId,
            [FromQuery] List<int>? areaScopeIds = null,
            [FromQuery] int? anio = null,
            [FromQuery] int? mes = null)
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)
                    ? uid : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                var filters = new GestionSalidaFiltersDto
                {
                    WorkerId           = workerId,
                    LugarProyectoId    = lugarProyectoId,
                    FilterAreaScopeIds = areaScopeIds,
                    CurrentUserId      = userId.Value,
                    SeesAllOverride    = User.IsInRole(Roles.UsuarioRecepcion),
                };
                var (pdfBytes, count) = await _service.RendirMes(filters, anio, mes, userId.Value);

                Response.Headers.Append("X-Rendidas-Count", count.ToString());
                Response.Headers.Append("Access-Control-Expose-Headers", "X-Rendidas-Count, Content-Disposition");

                var filename = $"Planilla_Rendicion_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
                return File(pdfBytes, "application/pdf", filename);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionSalidaController.RendirMes");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        // ── Reembolso ────────────────────────────────────────────────────────


        /// <summary>UserId del token, o null si el claim no viene o no es numérico.</summary>
        private int? GetUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : (int?)null;
    }
}
