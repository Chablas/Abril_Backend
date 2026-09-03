using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Application.Dtos;
using Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Presentation;

[Authorize]
[ApiController]
[Route("api/v1/arquitectura-comercial/costos")]
[RequireFeature("arquitectura-comercial.costos")]
public class CostosController : ControllerBase
{
    private readonly ICostoService _service;
    private readonly ILogger<CostosController> _logger;

    public CostosController(ICostoService service, ILogger<CostosController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private string? UsuarioActual => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet("filtros")]
    public async Task<IActionResult> GetFiltros()
    {
        try
        {
            return Ok(await _service.GetFiltros());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CostosController.GetFiltros");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("matriz")]
    public async Task<IActionResult> GetMatriz([FromQuery] int proyectoId, [FromQuery] int anio, [FromQuery] int mes)
    {
        try
        {
            return Ok(await _service.GetMatriz(proyectoId, anio, mes));
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CostosController.GetMatriz");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpPost("registro")]
    public async Task<IActionResult> UpsertRegistro([FromBody] UpsertCostoRegistroDTO body)
    {
        try
        {
            await _service.UpsertRegistro(body, UsuarioActual);
            return Ok(new { message = "Guardado." });
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CostosController.UpsertRegistro");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpPost("proyeccion")]
    public async Task<IActionResult> UpsertProyeccion([FromBody] UpsertCostoProyeccionDTO body)
    {
        try
        {
            await _service.UpsertProyeccion(body, UsuarioActual);
            return Ok(new { message = "Guardado." });
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CostosController.UpsertProyeccion");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] int anio, [FromQuery] int mes)
    {
        try
        {
            return Ok(await _service.GetDashboard(anio, mes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CostosController.GetDashboard");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("evolucion")]
    public async Task<IActionResult> GetEvolucion([FromQuery] int anioDesde, [FromQuery] int mesDesde, [FromQuery] int cantidadMeses = 12)
    {
        try
        {
            return Ok(await _service.GetEvolucion(anioDesde, mesDesde, cantidadMeses));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CostosController.GetEvolucion");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpPost("meta")]
    [RequireFeature("arquitectura-comercial.costos.configurar")]
    public async Task<IActionResult> UpsertMeta([FromBody] UpsertCostoMetaDTO body)
    {
        try
        {
            await _service.UpsertMeta(body, UsuarioActual);
            return Ok(new { message = "Guardado." });
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en CostosController.UpsertMeta");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }
}
