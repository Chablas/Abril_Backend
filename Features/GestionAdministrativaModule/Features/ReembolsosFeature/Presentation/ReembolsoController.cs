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
    /// "Reembolsos": la bandeja de Tesorería. Basta con el rol TESORERO del token; la categoría
    /// del puesto ya no se exige para entrar.
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
            [FromQuery] string? q,
            [FromQuery] string? estadoReembolso,
            [FromQuery] List<int>? areaScopeIds = null,
            [FromQuery] int? periodoAnio = null,
            [FromQuery] int? periodoMes = null)
        {
            try
            {
                return Ok(await _service.GetAll(
                    Filtros(workerId, q, estadoReembolso, areaScopeIds, periodoAnio, periodoMes)));
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
                return Ok(await _service.GetFilterData());
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
                return Ok(await _service.GetDetalle(id));
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

        /// <summary>
        /// Seguimiento de pagos por colaborador. Va aparte del listado porque es la otra vista de
        /// la pantalla y se arma distinto: agrupa por persona en vez de por planilla.
        /// </summary>
        [HttpGet("seguimiento")]
        public async Task<IActionResult> GetSeguimiento(
            [FromQuery] int? workerId,
            [FromQuery] string? q,
            [FromQuery] List<int>? areaScopeIds = null,
            [FromQuery] int? periodoAnio = null,
            [FromQuery] int? periodoMes = null)
        {
            try
            {
                return Ok(await _service.GetSeguimiento(
                    Filtros(workerId, q, null, areaScopeIds, periodoAnio, periodoMes)));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReembolsoController.GetSeguimiento");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Paso 1 de Tesorería: confirmar la revisión documental (RG-26).</summary>
        [HttpPatch("confirmar-revision")]
        public async Task<IActionResult> ConfirmarRevision([FromBody] ReembolsoSeleccionDto dto)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.ConfirmarRevision(dto, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReembolsoController.ConfirmarRevision");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Paso 2 de Tesorería: registrar el pago y cerrar el ciclo.</summary>
        [HttpPatch("pagar")]
        public async Task<IActionResult> MarcarPagadas([FromBody] ReembolsoSeleccionDto dto)
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

        /// <summary>
        /// A quién le llegaría el aviso de pago de lo seleccionado. Lo piden las confirmaciones
        /// (el botón masivo y el del modal de detalle) para nombrar las direcciones reales. No hay
        /// preview para confirmar la revisión: ese paso no manda ningún correo.
        /// </summary>
        [HttpPost("pagar/correo-preview")]
        public async Task<IActionResult> GetCorreoPreviewPago([FromBody] ReembolsoSeleccionDto dto)
        {
            try
            {
                return Ok(await _service.GetCorreoPreviewPago(dto));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ReembolsoController.GetCorreoPreviewPago");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        private static ReembolsoFiltersDto Filtros(
            int? workerId, string? texto, string? estadoReembolso,
            List<int>? areaScopeIds, int? periodoAnio, int? periodoMes) =>
            new()
            {
                WorkerId           = workerId,
                Texto              = texto,
                EstadoReembolso    = estadoReembolso,
                FilterAreaScopeIds = areaScopeIds,
                PeriodoAnio        = periodoAnio,
                PeriodoMes         = periodoMes,
            };
    }
}
