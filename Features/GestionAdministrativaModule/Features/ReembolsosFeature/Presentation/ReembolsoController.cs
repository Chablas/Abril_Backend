using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Reembolsos.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionAdministrativa.Reembolsos.Presentation
{
    /// <summary>
    /// "Reembolsos": la bandeja de Tesorería. El rol se exige acá (token) y la categoría del puesto
    /// la verifica el servicio contra la base — hacen falta las dos.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/reembolsos")]
    [Authorize(Roles = Roles.Tesorero)]
    public class ReembolsoController : ControllerBase
    {
        private readonly IReembolsoService _service;
        private readonly ILogger<ReembolsoController> _logger;

        public ReembolsoController(IReembolsoService service, ILogger<ReembolsoController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? workerId,
            [FromQuery] string? estadoReembolso,
            [FromQuery] List<int>? areaScopeIds = null,
            [FromQuery] int? periodoAnio = null,
            [FromQuery] int? periodoMes = null)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });

                var filters = new ReembolsoFiltersDto
                {
                    WorkerId           = workerId,
                    EstadoReembolso    = estadoReembolso,
                    FilterAreaScopeIds = areaScopeIds,
                    PeriodoAnio        = periodoAnio,
                    PeriodoMes         = periodoMes,
                };
                return Ok(await _service.GetAll(filters, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReembolsoController.GetAll");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("filter-data")]
        public async Task<IActionResult> GetFilterData()
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.GetFilterData(userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReembolsoController.GetFilterData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("{id:int}/detalle")]
        public async Task<IActionResult> GetDetalle(int id)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.GetDetalle(id, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReembolsoController.GetDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("pagar")]
        public async Task<IActionResult> MarcarPagadas([FromBody] PagarDto dto)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.MarcarPagadas(dto, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReembolsoController.MarcarPagadas");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
