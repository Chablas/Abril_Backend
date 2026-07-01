using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.BoletinModule.BirthdayClubFeature.Presentation
{
    [ApiController]
    [Route("api/v1/boletin/cumpleanos")]
    [Authorize]
    public class CumpleanosController : ControllerBase
    {
        private readonly ICumpleanosService _service;
        private readonly ILogger<CumpleanosController> _logger;

        public CumpleanosController(ICumpleanosService service, ILogger<CumpleanosController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Cumpleañeros del trimestre (1-4) con sus fotos. Una sola petición trae datos + fotos
        /// para que el hover del calendario sea instantáneo.
        /// </summary>
        [HttpGet("trimestre/{trimestre:int}")]
        public async Task<IActionResult> GetTrimestre(int trimestre)
        {
            try { return Ok(await _service.GetTrimestre(trimestre)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CumpleanosController.GetTrimestre");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
