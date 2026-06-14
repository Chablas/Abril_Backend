using Abril_Backend.Features.SsomaModule.OptFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.OptFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.OptFeature.Presentation;

[ApiController]
[Route("api/v1/ssoma-opt")]
[AllowAnonymous]
public class OptController : ControllerBase
{
    private readonly IOptService _service;
    private readonly ILogger<OptController> _logger;

    public OptController(IOptService service, ILogger<OptController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    [HttpGet("catalogos")]
    public async Task<IActionResult> GetCatalogos()
    {
        try
        {
            var pets      = await _service.GetPetsAsync();
            var criterios = await _service.GetCriteriosVerificacionAsync();
            return Ok(new { pets, criterios });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en OptController.GetCatalogos");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? proyectoId,
        [FromQuery] int? petId,
        [FromQuery] string? tipoObservacion,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int? trabajadorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            return Ok(await _service.GetListAsync(
                proyectoId, petId, tipoObservacion, fechaDesde, fechaHasta, trabajadorId, page, pageSize));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en OptController.GetList");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int? proyectoId,
        [FromQuery] int? anio)
    {
        try
        {
            return Ok(await _service.GetDashboardAsync(proyectoId, anio));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en OptController.GetDashboard");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        try
        {
            var detalle = await _service.GetDetalleAsync(id);
            return Ok(detalle);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"OPT {id} no encontrado." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en OptController.GetDetalle");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearOptRequest request)
    {
        try
        {
            var id = await _service.CrearOptAsync(request);
            return StatusCode(201, new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en OptController.Crear");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }
}
