using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Presentation
{
    [ApiController]
    [Route("api/v1/gestion-administrativa/configuracion/revisor-salidas")]
    [Authorize]
    public class RevisorSalidaController : ControllerBase
    {
        private readonly IRevisorSalidaService _service;
        private readonly ILogger<RevisorSalidaController> _logger;

        public RevisorSalidaController(IRevisorSalidaService service, ILogger<RevisorSalidaController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>Carga inicial: trabajadores con sus n revisores + opciones + árbol de áreas.</summary>
        [HttpGet]
        public async Task<IActionResult> GetInitialData()
        {
            try   { return Ok(await _service.GetInitialDataAsync()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RevisorSalidaController.GetInitialData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Reemplaza el conjunto de revisores de un trabajador.</summary>
        [HttpPut("{workerId:int}")]
        public async Task<IActionResult> UpdateRevisores(int workerId, [FromBody] WorkerRevisoresUpdateDto dto)
        {
            try
            {
                await _service.UpdateWorkerRevisoresAsync(workerId, dto?.Revisores ?? new List<WorkerRevisorAsignacionDto>());
                return Ok(new { message = "Revisores de salidas actualizados exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RevisorSalidaController.UpdateRevisores");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
