using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.RevisionDescansos;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/revision-descansos")]
    [Authorize]
    [RequireFeature("ssoma.salud-ocupacional.revision-descansos")]
    public class RevisionDescansosController : ControllerBase
    {
        private readonly IRevisionDescansosService _service;
        private readonly ILogger<RevisionDescansosController> _logger;

        public RevisionDescansosController(IRevisionDescansosService service, ILogger<RevisionDescansosController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int? CurrentUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        /// <summary>Carga inicial: datos de filtros (áreas + trabajadores) + primera página, en un solo roundtrip.</summary>
        [HttpGet("init")]
        public async Task<IActionResult> GetInit([FromQuery] RevisionDescansosFiltroDto filtro)
        {
            try { return Ok(await _service.GetInit(filtro)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en RevisionDescansosController.GetInit"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Solo la tabla filtrada/ordenada/paginada (los filtros ya están cargados).</summary>
        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] RevisionDescansosFiltroDto filtro)
        {
            try { return Ok(await _service.ListPaged(filtro)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en RevisionDescansosController.GetList"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetDetalle(int id)
        {
            try { return Ok(await _service.GetDetalle(id)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en RevisionDescansosController.GetDetalle"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Aprueba en bloque las solicitudes pendientes seleccionadas (sirve también para una sola).</summary>
        [HttpPost("aprobar")]
        public async Task<IActionResult> Aprobar([FromBody] RevisionDescansosAprobarDto dto)
        {
            try
            {
                var count = await _service.Aprobar(dto, CurrentUserId());
                return Ok(new { count, message = $"{count} solicitud(es) aprobada(s) exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en RevisionDescansosController.Aprobar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        /// <summary>Rechaza en bloque las solicitudes pendientes seleccionadas con un motivo común.</summary>
        [HttpPost("rechazar")]
        public async Task<IActionResult> Rechazar([FromBody] RevisionDescansosRechazarDto dto)
        {
            try
            {
                var count = await _service.Rechazar(dto, CurrentUserId());
                return Ok(new { count, message = $"{count} solicitud(es) rechazada(s)." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en RevisionDescansosController.Rechazar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
