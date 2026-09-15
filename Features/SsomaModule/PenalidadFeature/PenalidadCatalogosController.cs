using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.Penalidad.Dtos;
using Abril_Backend.Features.Ssoma.Penalidad.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.Ssoma.Penalidad;

/// <summary>
/// CRUD admin de los catálogos que hasta ahora se creaban vacíos y nunca se administraban desde
/// la UI: Infracciones (tipificación + factor UIT/monto fijo) y el valor de la UIT por año.
/// </summary>
[ApiController]
[Route("api/v1/ssoma-penalidad-catalogos")]
[Authorize]
[RequireFeature("ssoma.gestion.penalidades.catalogos")]
public class PenalidadCatalogosController : ControllerBase
{
    private readonly IPenalidadService _service;
    private readonly ILogger<PenalidadCatalogosController> _logger;

    public PenalidadCatalogosController(IPenalidadService service, ILogger<PenalidadCatalogosController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    // GetInfracciones (lectura del combo de tipificación, siempre soloActivas=true) vive en
    // PenalidadController — lo necesita cualquier usuario con acceso a Penalidades, no solo el
    // admin del catálogo. Este GET es distinto: para administrar el catálogo hay que poder ver
    // también las infracciones inactivas (soloActivas=false).
    [HttpGet("infracciones")]
    public async Task<IActionResult> GetInfraccionesAdmin([FromQuery] bool soloActivas = false)
    {
        try { return Ok(await _service.GetInfraccionesAsync(soloActivas)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadCatalogosController.GetInfraccionesAdmin"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("infracciones")]
    public async Task<IActionResult> CrearInfraccion([FromBody] InfraccionUpsertRequest req)
    {
        try { return StatusCode(201, await _service.CrearInfraccionAsync(req)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadCatalogosController.CrearInfraccion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPut("infracciones/{id:int}")]
    public async Task<IActionResult> ActualizarInfraccion(int id, [FromBody] InfraccionUpsertRequest req)
    {
        try { return Ok(await _service.ActualizarInfraccionAsync(id, req)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadCatalogosController.ActualizarInfraccion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("uit")]
    public async Task<IActionResult> GetUitAnios()
    {
        try { return Ok(await _service.GetUitAniosAsync()); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadCatalogosController.GetUitAnios"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("uit")]
    public async Task<IActionResult> CrearUitAnio([FromBody] UitAnioUpsertRequest req)
    {
        try { return StatusCode(201, await _service.CrearUitAnioAsync(req)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadCatalogosController.CrearUitAnio"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPut("uit/{id:int}")]
    public async Task<IActionResult> ActualizarUitAnio(int id, [FromBody] UitAnioUpsertRequest req)
    {
        try { return Ok(await _service.ActualizarUitAnioAsync(id, req)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadCatalogosController.ActualizarUitAnio"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }
}
