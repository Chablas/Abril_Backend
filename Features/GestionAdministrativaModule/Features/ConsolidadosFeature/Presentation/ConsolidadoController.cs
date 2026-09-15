using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Consolidados.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionAdministrativa.Consolidados.Presentation
{
    /// <summary>
    /// "Consolidados": los Consolidados del S10 del alcance del usuario y la decisión del reembolso
    /// sobre cada uno. Gestión de Rendiciones llega hasta adjuntar el documento; el pago es de
    /// Tesorería y vive en Reembolsos.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/consolidados")]
    [Authorize]
    public class ConsolidadoController : ControllerBase
    {
        private readonly IConsolidadoService _service;
        private readonly ILogger<ConsolidadoController> _logger;

        public ConsolidadoController(IConsolidadoService service, ILogger<ConsolidadoController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;

        /// <summary>Alcance del usuario: lo mismo que arman las otras bandejas en cada petición.</summary>
        private ConsolidadoFiltersDto Scope() => new()
        {
            CurrentUserId   = CurrentUserId,
            SeesAllOverride = User.IsInRole(Roles.UsuarioRecepcion),
        };

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? workerId,
            [FromQuery] string? estadoReembolso,
            [FromQuery] string? texto,
            [FromQuery] List<int>? areaScopeIds = null,
            [FromQuery] int? periodoAnio = null,
            [FromQuery] int? periodoMes = null)
        {
            try
            {
                var filters = Scope();
                filters.WorkerId           = workerId;
                filters.EstadoReembolso    = estadoReembolso;
                filters.Texto              = texto;
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
                _logger.LogError(ex, "Error en ConsolidadoController.GetAll");
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
                _logger.LogError(ex, "Error en ConsolidadoController.GetFilterData");
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
                _logger.LogError(ex, "Error en ConsolidadoController.GetDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Los correos que saldrían al decidir el reembolso de la selección enviada, con sus
        /// destinatarios reales. Lo piden las confirmaciones para nombrar las direcciones en vez de
        /// prometer un correo genérico. Es POST y no GET porque la selección viaja en el cuerpo.
        /// </summary>
        [HttpPost("correo-preview")]
        public async Task<IActionResult> GetCorreoPreview([FromBody] ConsolidadoCorreoPreviewRequestDto dto)
        {
            try
            {
                return Ok(await _service.GetCorreoPreview(dto, Scope()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConsolidadoController.GetCorreoPreview");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Aprueba el reembolso, que ES firmarlo: estampa la firma en la planilla y en el
        /// Consolidado del S10 y lo manda a la bandeja de Tesorería.
        /// </summary>
        [HttpPatch("reembolso/aprobar")]
        public Task<IActionResult> AprobarReembolso([FromBody] ConsolidadoAccionDto dto) =>
            DecidirAsync(dto, aprobar: true, nameof(AprobarReembolso));

        /// <summary>
        /// Observa el reembolso: vuelve al trabajador para que subsane. La observación es
        /// obligatoria — es lo que él lee para saber qué corregir, y lo que se le manda al
        /// Coordinador ERP si la corrección tiene que hacerse dentro del S10.
        /// </summary>
        [HttpPatch("reembolso/observar")]
        public Task<IActionResult> ObservarReembolso([FromBody] ConsolidadoAccionDto dto) =>
            DecidirAsync(dto, aprobar: false, nameof(ObservarReembolso));

        private async Task<IActionResult> DecidirAsync(ConsolidadoAccionDto dto, bool aprobar, string accion)
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
                _logger.LogError(ex, "Error en ConsolidadoController.{Accion}", accion);
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
