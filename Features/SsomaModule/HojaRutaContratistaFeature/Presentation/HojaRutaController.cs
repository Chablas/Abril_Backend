using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.HojaRutaContratistaFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.HojaRutaContratistaFeature.Presentation;

[ApiController]
[Route("api/v1/ssoma/hoja-ruta")]
[Authorize]
public class HojaRutaController : ControllerBase
{
    private readonly IHojaRutaService _service;
    private readonly ILogger<HojaRutaController> _logger;

    public HojaRutaController(IHojaRutaService service, ILogger<HojaRutaController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("contratistas")]
    public async Task<IActionResult> GetContratistasActivos([FromQuery] int proyectoId)
    {
        try
        {
            var contratistas = await _service.GetContratistasActivosAsync(proyectoId);
            return Ok(contratistas);
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en HojaRutaController.GetContratistasActivos");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetResumen(
        [FromQuery] int contributorId,
        [FromQuery] int proyectoId,
        [FromQuery] int anio,
        [FromQuery] int numeroSemana)
    {
        try
        {
            if (numeroSemana is < 1 or > 53)
                return BadRequest(new { message = "Número de semana inválido." });

            var resumen = await _service.GetResumenAsync(contributorId, proyectoId, anio, numeroSemana);
            return Ok(resumen);
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en HojaRutaController.GetResumen");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }
}
