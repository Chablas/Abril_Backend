using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces;
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
        public async Task<IActionResult> GetAll([FromQuery] int? workerId, [FromQuery] int? lugarProyectoId)
        {
            try
            {
                var filters = new GestionSalidaFiltersDto
                {
                    WorkerId        = workerId,
                    LugarProyectoId = lugarProyectoId,
                };
                return Ok(await _service.GetAll(filters));
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
        public async Task<IActionResult> ExportarExcel([FromQuery] int? workerId, [FromQuery] int? lugarProyectoId)
        {
            try
            {
                var filters = new GestionSalidaFiltersDto
                {
                    WorkerId        = workerId,
                    LugarProyectoId = lugarProyectoId,
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

        [HttpGet("filter-data")]
        public async Task<IActionResult> GetFilterData()
        {
            try
            {
                return Ok(await _service.GetFilterData());
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
    }
}
