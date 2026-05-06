using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/alertas")]
    public class EmoAlertaController : ControllerBase
    {
        private readonly IEmoAlertaService _service;
        private readonly IEmoAutoProgramacionService _autoProgramacionService;
        private readonly IConfiguration _configuration;

        public EmoAlertaController(
            IEmoAlertaService service,
            IEmoAutoProgramacionService autoProgramacionService,
            IConfiguration configuration)
        {
            _service = service;
            _autoProgramacionService = autoProgramacionService;
            _configuration = configuration;
        }

        [HttpGet("procesar")]
        public async Task<IActionResult> Procesar()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader != $"Bearer {_configuration["CronSecret"]}")
                    return Unauthorized();

                var result = await _service.ProcesarAlertas();
                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("auto-programar")]
        public async Task<IActionResult> AutoProgramar()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader != $"Bearer {_configuration["CronSecret"]}")
                    return Unauthorized();

                var result = await _autoProgramacionService.ProcesarAutoProgramacion();
                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
