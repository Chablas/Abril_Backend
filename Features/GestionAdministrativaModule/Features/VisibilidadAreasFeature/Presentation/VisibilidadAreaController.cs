using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Presentation
{
    /// <summary>
    /// Override manual de visibilidad por área. El segmento <c>{ambito}</c> dice de qué bandeja se
    /// está configurando: <c>salidas</c> (Gestión de Salidas) o <c>rendiciones</c> (Gestión de
    /// Rendiciones). Cada una se administra desde la Configuración de su propia pantalla y las dos
    /// conviven en la misma tabla sin pisarse.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/configuracion/visibilidad/{ambito}")]
    [Authorize]
    public class VisibilidadAreaController : ControllerBase
    {
        private readonly IVisibilidadAreaService _service;
        private readonly ILogger<VisibilidadAreaController> _logger;

        public VisibilidadAreaController(IVisibilidadAreaService service, ILogger<VisibilidadAreaController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>
        /// Traduce el segmento de la URL al id del catálogo. Un ámbito desconocido es un 404 y no
        /// un default silencioso: configurar la visibilidad de la pantalla equivocada no se nota.
        /// </summary>
        private static int AmbitoId(string ambito) => ambito?.Trim().ToLowerInvariant() switch
        {
            "salidas"     => VisibilidadAmbitoIds.Salidas,
            "rendiciones" => VisibilidadAmbitoIds.Rendiciones,
            _ => throw new AbrilException("Ámbito de visibilidad desconocido.", 404),
        };

        [HttpGet]
        public async Task<IActionResult> GetInitialData(string ambito)
        {
            try   { return Ok(await _service.GetInitialDataAsync(AmbitoId(ambito))); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en VisibilidadAreaController.GetInitialData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("area-scope-tree")]
        public async Task<IActionResult> GetAreaTree(string ambito)
        {
            try
            {
                AmbitoId(ambito);
                return Ok(await _service.GetAreaTreeAsync());
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en VisibilidadAreaController.GetAreaTree");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("worker/{workerId:int}")]
        public async Task<IActionResult> GetWorkerAsignaciones(string ambito, int workerId)
        {
            try   { return Ok(await _service.GetWorkerAsignacionesAsync(AmbitoId(ambito), workerId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en VisibilidadAreaController.GetWorkerAsignaciones");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPut("worker/{workerId:int}")]
        public async Task<IActionResult> UpdateWorkerAsignaciones(
            string ambito, int workerId, [FromBody] VisibilidadUpdateDto dto)
        {
            try
            {
                await _service.UpdateWorkerAsignacionesAsync(
                    AmbitoId(ambito), workerId, dto?.Areas ?? new List<VisibilidadAsignacionDto>());
                return Ok(new { message = "Visibilidad actualizada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en VisibilidadAreaController.UpdateWorkerAsignaciones");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
