using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Presentation
{
    [ApiController]
    [Route("api/v1/gestion-administrativa/configuracion/revisores-areas")]
    [Authorize]
    public class AreaRevisorController : ControllerBase
    {
        private readonly IAreaRevisorService _service;
        private readonly ILogger<AreaRevisorController> _logger;

        public AreaRevisorController(IAreaRevisorService service, ILogger<AreaRevisorController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>Carga inicial: áreas estándar (primer nodo de cada rama) con sus n revisores + opciones.</summary>
        [HttpGet]
        public async Task<IActionResult> GetInitialData()
        {
            try   { return Ok(await _service.GetInitialDataAsync()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AreaRevisorController.GetInitialData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Reemplaza el conjunto de revisores de un área (nodo area_scope).</summary>
        [HttpPut("{areaScopeId:int}")]
        public async Task<IActionResult> UpdateRevisores(int areaScopeId, [FromBody] AreaRevisoresUpdateDto dto)
        {
            try
            {
                await _service.UpdateAreaRevisoresAsync(areaScopeId, dto?.Revisores ?? new List<AreaRevisorAsignacionDto>());
                return Ok(new { message = "Revisores del área actualizados exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AreaRevisorController.UpdateRevisores");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
