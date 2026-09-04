using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionAdministrativa.GestionRendiciones.Presentation
{
    /// <summary>
    /// "Gestión de Rendiciones": las planillas del alcance del revisor y todo lo que va desde el
    /// Consolidado del S10 en adelante (adjuntarlo, decidir el reembolso, firmar). El pago es de
    /// Tesorería y vive en Reembolsos.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/gestion-rendiciones")]
    [Authorize]
    public class GestionRendicionController : ControllerBase
    {
        private readonly IGestionRendicionService _service;
        private readonly ILogger<GestionRendicionController> _logger;

        public GestionRendicionController(
            IGestionRendicionService service, ILogger<GestionRendicionController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;

        /// <summary>Alcance del usuario: lo mismo que arma Gestión de Salidas para cada petición.</summary>
        private GestionRendicionFiltersDto Scope() => new()
        {
            CurrentUserId   = CurrentUserId,
            SeesAllOverride = User.IsInRole(Roles.UsuarioRecepcion),
        };

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? workerId,
            [FromQuery] string? estadoReembolso,
            [FromQuery] bool? conConsolidado,
            [FromQuery] List<int>? areaScopeIds = null,
            [FromQuery] int? periodoAnio = null,
            [FromQuery] int? periodoMes = null)
        {
            try
            {
                var filters = Scope();
                filters.WorkerId           = workerId;
                filters.EstadoReembolso    = estadoReembolso;
                filters.ConConsolidado     = conConsolidado;
                filters.FilterAreaScopeIds = areaScopeIds;
                filters.PeriodoAnio        = periodoAnio;
                filters.PeriodoMes         = periodoMes;

                return Ok(await _service.GetAll(filters));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionRendicionController.GetAll");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("filter-data")]
        public async Task<IActionResult> GetFilterData()
        {
            try
            {
                return Ok(await _service.GetFilterData(Scope()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionRendicionController.GetFilterData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("{id:int}/detalle")]
        public async Task<IActionResult> GetDetalle(int id)
        {
            try
            {
                return Ok(await _service.GetDetalle(id, Scope()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionRendicionController.GetDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("{id:int}/consolidado-s10")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(25 * 1024 * 1024)]
        public async Task<IActionResult> UploadConsolidadoS10(int id, [FromForm] IFormFile file)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.UploadConsolidadoS10(id, file, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionRendicionController.UploadConsolidadoS10");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("reembolso/aprobar")]
        public Task<IActionResult> AprobarReembolso([FromBody] ReembolsoAccionDto dto) =>
            DecidirAsync(dto, aprobar: true, nameof(AprobarReembolso));

        [HttpPatch("reembolso/rechazar")]
        public Task<IActionResult> RechazarReembolso([FromBody] ReembolsoAccionDto dto) =>
            DecidirAsync(dto, aprobar: false, nameof(RechazarReembolso));

        private async Task<IActionResult> DecidirAsync(ReembolsoAccionDto dto, bool aprobar, string accion)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.DecidirReembolso(dto, aprobar, Scope(), userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionRendicionController.{Accion}", accion);
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("reembolso/firmar")]
        public async Task<IActionResult> Firmar([FromBody] ReembolsoAccionDto dto)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.Firmar(dto, Scope(), userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GestionRendicionController.Firmar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
