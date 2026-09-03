using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Dtos;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Presentation;

[Authorize]
[ApiController]
[Route("api/v1/almacen/materiales")]
[RequireFeature("almacen.materiales")]
public class MaterialesController : ControllerBase
{
    private readonly IMaterialService _service;
    private readonly ILogger<MaterialesController> _logger;

    public MaterialesController(IMaterialService service, ILogger<MaterialesController> logger)
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
            _logger.LogError(ex, "Error en MaterialesController.GetFiltros");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateMaterial([FromBody] CreateAlmacenMaterialDTO body)
    {
        try
        {
            return Ok(await _service.CreateMaterial(body));
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en MaterialesController.CreateMaterial");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("/api/v1/almacen/movimientos")]
    public async Task<IActionResult> GetMovimientos(
        [FromQuery] int? proyectoId,
        [FromQuery] int? materialId,
        [FromQuery] string? tipo,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 20)
    {
        try
        {
            var query = new AlmacenMovimientosQueryParams
            {
                ProyectoId = proyectoId,
                MaterialId = materialId,
                Tipo = tipo,
                Desde = desde,
                Hasta = hasta,
                Pagina = pagina,
                PorPagina = porPagina
            };
            return Ok(await _service.GetMovimientos(query));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en MaterialesController.GetMovimientos");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpPost("/api/v1/almacen/movimientos")]
    public async Task<IActionResult> CreateMovimiento([FromBody] CreateAlmacenMovimientoDTO body)
    {
        try
        {
            return Ok(await _service.CreateMovimiento(body, UsuarioActual));
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en MaterialesController.CreateMovimiento");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("/api/v1/almacen/stock")]
    public async Task<IActionResult> GetStock([FromQuery] int? proyectoId)
    {
        try
        {
            return Ok(await _service.GetStock(proyectoId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en MaterialesController.GetStock");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpGet("/api/v1/almacen/dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] int? proyectoId, [FromQuery] int diasVentana = 90)
    {
        try
        {
            return Ok(await _service.GetDashboard(proyectoId, diasVentana));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en MaterialesController.GetDashboard");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }
}
