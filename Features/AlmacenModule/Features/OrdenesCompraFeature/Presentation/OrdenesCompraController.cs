using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Dtos;
using Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Presentation;

[Authorize]
[ApiController]
[Route("api/v1/almacen/ordenes-compra")]
[RequireFeature("almacen.ordenes-compra")]
public class OrdenesCompraController : ControllerBase
{
    private readonly IOrdenCompraService _service;
    private readonly ILogger<OrdenesCompraController> _logger;

    public OrdenesCompraController(IOrdenCompraService service, ILogger<OrdenesCompraController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private string? UsuarioActual => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpGet]
    public async Task<IActionResult> GetOrdenesCompra(
        [FromQuery] int? proyectoId,
        [FromQuery] string? tipo,
        [FromQuery] string? search,
        [FromQuery] int pagina = 1,
        [FromQuery] int porPagina = 20)
    {
        try
        {
            var query = new AlmacenOrdenCompraQueryParams { ProyectoId = proyectoId, Tipo = tipo, Search = search, Pagina = pagina, PorPagina = porPagina };
            return Ok(await _service.GetOrdenesCompra(query));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en OrdenesCompraController.GetOrdenesCompra");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> CreateOrdenCompra([FromForm] CreateAlmacenOrdenCompraDTO body, IFormFile archivo)
    {
        try
        {
            if (archivo == null || archivo.Length == 0) return BadRequest(new { message = "Debe adjuntar el archivo de la orden de compra o contrato." });

            using var stream = archivo.OpenReadStream();
            var result = await _service.CreateOrdenCompra(body, stream, archivo.FileName, UsuarioActual);
            return Ok(result);
        }
        catch (AbrilException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en OrdenesCompraController.CreateOrdenCompra");
            return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
        }
    }
}
