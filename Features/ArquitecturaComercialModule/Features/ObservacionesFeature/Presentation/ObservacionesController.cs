using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Application.Dtos;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Presentation;

[Authorize]
[ApiController]
[Route("api/v1/arquitectura-comercial/observaciones")]
public class ObservacionesController : ControllerBase
{
    private readonly IObservacionService _service;
    private readonly ILogger<ObservacionesController> _logger;

    public ObservacionesController(IObservacionService service, ILogger<ObservacionesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetObservaciones(
        [FromQuery] int? proyectoId,
        [FromQuery] string? estado,
        [FromQuery] string? partida,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] string? search,
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 20)
    {
        try
        {
            var result = await _service.GetObservaciones(proyectoId, estado, partida, desde, hasta, search, pagina, porPagina);
            return Ok(result);
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _service.GetObservacionById(id);
            if (result == null) return NotFound(new { message = "No se encontró la observación." });
            return Ok(result);
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("filtros")]
    public async Task<IActionResult> GetFiltros()
    {
        try
        {
            var result = await _service.GetFiltros();
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int? proyectoId)
    {
        try
        {
            var result = await _service.GetDashboard(desde, hasta, proyectoId);
            return Ok(result);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> CreateObservacion([FromForm] CreateObservacionDTO body, IFormFile? foto)
    {
        try
        {
            Stream? stream = foto != null ? foto.OpenReadStream() : null;
            var result = await _service.CreateObservacion(body, stream, foto?.FileName);
            return Ok(result);
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpPost("{id:int}/levantar")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Levantar(int id, [FromForm] LevantarObservacionDTO body, IFormFile? foto)
    {
        try
        {
            Stream? stream = foto != null ? foto.OpenReadStream() : null;
            var result = await _service.LevantarObservacion(id, stream, foto?.FileName, body);
            if (result == null) return NotFound(new { message = "No se encontró la observación." });
            return Ok(result);
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }
}
