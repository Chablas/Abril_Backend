using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/workers")]
    [Authorize]
    public class WorkersController : ControllerBase
    {
        private readonly IWorkerSearchService _service;
        private readonly ILogger<WorkersController> _logger;

        public WorkersController(IWorkerSearchService service, ILogger<WorkersController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 20)
        {
            try { return Ok(await _service.Search(q, limit)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en WorkersController.Search");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
