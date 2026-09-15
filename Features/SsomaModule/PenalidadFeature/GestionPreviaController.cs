using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.Penalidad.Dtos;
using Abril_Backend.Features.Ssoma.Penalidad.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.Ssoma.Penalidad;

/// <summary>
/// Bitácora de gestión previa (correos/cartas/reuniones antes de penalizar formalmente) +
/// contexto de una empresa (esa bitácora + reincidencia de penalidades) para la tipificación.
/// </summary>
[ApiController]
[Route("api/v1/ssoma-penalidad-gestion-previa")]
[Authorize]
[RequireFeature("ssoma.gestion.penalidades.crear")]
public class GestionPreviaController : ControllerBase
{
    private readonly IGestionPreviaService _service;
    private readonly ILogger<GestionPreviaController> _logger;

    public GestionPreviaController(IGestionPreviaService service, ILogger<GestionPreviaController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private int GetUserId() =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] int empresaId)
    {
        try { return Ok(await _service.GetListAsync(empresaId)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en GestionPreviaController.GetList"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("contexto/{empresaId:int}")]
    public async Task<IActionResult> GetContexto(int empresaId)
    {
        try { return Ok(await _service.GetContextoEmpresaAsync(empresaId)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en GestionPreviaController.GetContexto"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost]
    public async Task<IActionResult> Registrar([FromBody] GestionPreviaRegistrarRequest req)
    {
        try { return StatusCode(201, await _service.RegistrarAsync(req, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en GestionPreviaController.Registrar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("documentos")]
    [RequestSizeLimit(20_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirAdjunto([FromForm] int empresaId, [FromForm] Microsoft.AspNetCore.Http.IFormFile file)
    {
        try
        {
            var url = await _service.SubirAdjuntoAsync(empresaId, file);
            return Ok(new { url });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en GestionPreviaController.SubirAdjunto"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }
}
