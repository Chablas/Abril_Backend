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
    /// "Consolidados": los Consolidados del S10 del alcance del usuario. La jefatura decide el
    /// reembolso sobre cada uno; el consolidador le avisa a la jefatura y le pide la corrección al
    /// Coordinador ERP. Gestión de Rendiciones llega hasta adjuntar el documento; el pago es de
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
        /// El detalle de una salida de los consolidados del alcance, en consulta: trayectos,
        /// capturas con sus montos y adjuntos. Lo abre el ojo de la tabla de salidas del detalle.
        /// </summary>
        [HttpGet("salidas/{solicitudId:int}/detalle")]
        public async Task<IActionResult> GetSalidaDetalle(int solicitudId)
        {
            try
            {
                return Ok(await _service.GetSalidaDetalle(solicitudId, Scope()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConsolidadoController.GetSalidaDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Los correos que saldrían con la acción indicada sobre la selección enviada (la decisión
        /// de la jefatura, el aviso a la jefatura o la corrección al ERP), con sus destinatarios
        /// reales. Lo piden las confirmaciones para nombrar las direcciones en vez de prometer un
        /// correo genérico. Es POST y no GET porque la selección viaja en el cuerpo.
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
        /// Observa el reembolso: vuelve al consolidador para que subsane. La observación es
        /// obligatoria — es lo que él lee para saber qué corregir, y lo que se le manda al
        /// Coordinador ERP si la corrección tiene que hacerse dentro del S10.
        /// </summary>
        [HttpPatch("reembolso/observar")]
        public Task<IActionResult> ObservarReembolso([FromBody] ConsolidadoAccionDto dto) =>
            DecidirAsync(dto, aprobar: false, nameof(ObservarReembolso));

        /// <summary>
        /// El consolidador le avisa a la jefatura que el consolidado tiene reembolsos esperando su
        /// visto bueno.
        /// </summary>
        [HttpPatch("{id:int}/notificar-jefatura")]
        public async Task<IActionResult> NotificarJefatura(int id)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(new { message = await _service.NotificarJefatura(id, Scope(), userId.Value) });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConsolidadoController.NotificarJefatura");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// El consolidador le pide al Coordinador ERP que corrija el Consolidado del S10 observado
        /// (§10.5 / RG-21). El motivo es obligatorio (CA-17) y viaja en el cuerpo.
        /// </summary>
        [HttpPost("{id:int}/correccion-s10")]
        public async Task<IActionResult> SolicitarCorreccionS10(int id, [FromBody] SolicitarCorreccionS10Dto dto)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(new
                {
                    message = await _service.SolicitarCorreccionS10(id, dto?.Motivo ?? string.Empty, Scope(), userId.Value),
                });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConsolidadoController.SolicitarCorreccionS10");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

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
