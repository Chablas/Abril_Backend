using Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Presentation;

[ApiController]
[Route("api/v1/ssoma-desempeno-supervisor")]
[Authorize]
public class DesempenoSupervisorController(DesempenoSupervisorRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int mes, [FromQuery] int anio)
    {
        if (mes < 1 || mes > 12 || anio < 2020)
            return BadRequest("Mes o año inválido.");
        try
        {
            var result = await repo.GetDesempenoAsync(mes, anio);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }
}
