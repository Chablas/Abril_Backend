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

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try   { return Ok(await _service.GetWorkerRevisoresAsync()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RevisorSalidaController.GetAll");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("options")]
        public async Task<IActionResult> GetOptions()
        {
            try   { return Ok(await _service.GetWorkerRevisorOptionsAsync()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RevisorSalidaController.GetOptions");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPut("{workerId:int}")]
        public async Task<IActionResult> UpdateRevisor(int workerId, [FromBody] WorkerRevisorSalidaUpdateDto dto)
        {
            try
            {
                await _service.UpdateWorkerRevisorAsync(workerId, dto.JefeWorkerId);
                return Ok(new { message = "Revisor de salidas actualizado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RevisorSalidaController.UpdateRevisor");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
