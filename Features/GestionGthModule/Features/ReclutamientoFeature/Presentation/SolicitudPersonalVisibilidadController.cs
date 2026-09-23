using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;
using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Presentation
{
    /// <summary>
    /// Solicitud de Personal → Configuración → Visibilidad: qué áreas ve cada trabajador en esa
    /// pantalla. Sin configuración propia lo resuelve el algoritmo de
    /// <c>SolicitudPersonalScopeResolver</c>; con ella, mandan las áreas marcadas.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-gth/solicitud-personal/configuracion/visibilidad")]
    [Authorize]
    [RequireFeature("gestion-gth.config.visibilidad-solicitud-personal")]
    public class SolicitudPersonalVisibilidadController : ControllerBase
    {
        private readonly ISolicitudPersonalVisibilidadService _service;
        private readonly ILogger<SolicitudPersonalVisibilidadController> _logger;

        public SolicitudPersonalVisibilidadController(
            ISolicitudPersonalVisibilidadService service,
            ILogger<SolicitudPersonalVisibilidadController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        private int? UserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        /// <summary>Carga de la sección: trabajadores + árbol de áreas, en una sola petición.</summary>
        [HttpGet]
        public async Task<IActionResult> GetInitialData()
        {
            try { return Ok(await _service.GetInitialData()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudPersonalVisibilidadController.GetInitialData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Lo configurado a mano del trabajador y lo que realmente ve hoy: los dos modales de la
        /// sección (detalle y edición) salen de esta sola llamada.
        /// </summary>
        [HttpGet("worker/{workerId:int}")]
        public async Task<IActionResult> GetWorkerDetalle(int workerId)
        {
            try { return Ok(await _service.GetWorkerDetalle(workerId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudPersonalVisibilidadController.GetWorkerDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPut("worker/{workerId:int}")]
        public async Task<IActionResult> UpdateWorkerAsignaciones(
            int workerId, [FromBody] SolicitudPersonalVisibilidadUpdateDto dto)
        {
            try
            {
                await _service.UpdateWorkerAsignaciones(workerId, dto?.Areas, UserId);
                return Ok(new { message = "Visibilidad actualizada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en SolicitudPersonalVisibilidadController.UpdateWorkerAsignaciones");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
