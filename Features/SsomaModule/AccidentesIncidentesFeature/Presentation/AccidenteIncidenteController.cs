using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.AccidentesIncidentesFeature.Presentation;

[ApiController]
[Route("api/v1/ssoma-accidentes-incidentes")]
[Authorize]
public class AccidenteIncidenteController : ControllerBase
{
    private readonly IAccidenteIncidenteService _service;
    private readonly ILogger<AccidenteIncidenteController> _logger;

    public AccidenteIncidenteController(IAccidenteIncidenteService service,
        ILogger<AccidenteIncidenteController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("inicializar")]
    public async Task<IActionResult> Inicializar()
    {
        try { return Ok(await _service.GetInicializarAsync()); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error inicializar flash report"); return StatusCode(500, new { message = "Error del servidor." }); }
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? proyectoId,
        [FromQuery] int? tipoId,
        [FromQuery] string? estado,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] bool? soloEnviados,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try { return Ok(await _service.GetListAsync(proyectoId, tipoId, estado, fechaDesde, fechaHasta, soloEnviados, page, pageSize)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error lista flash reports"); return StatusCode(500, new { message = "Error del servidor." }); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        try { return Ok(await _service.GetDetalleAsync(id)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error detalle flash report {Id}", id); return StatusCode(500, new { message = "Error del servidor." }); }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearFlashReportRequest request)
    {
        try
        {
            var usuarioId = ObtenerUsuarioId();
            var id = await _service.CrearAsync(request, usuarioId);
            return Ok(new { id, message = "Flash Report creado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error crear flash report"); return StatusCode(500, new { message = "Error del servidor." }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarFlashReportRequest request)
    {
        try
        {
            await _service.ActualizarAsync(id, request);
            return Ok(new { message = "Flash Report actualizado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error actualizar flash report {Id}", id); return StatusCode(500, new { message = "Error del servidor." }); }
    }

    [HttpPost("{id:int}/enviar")]
    public async Task<IActionResult> Enviar(int id)
    {
        try
        {
            await _service.EnviarFlashReportAsync(id);
            return Ok(new { message = "Flash Report enviado correctamente. Se generó el PDF y se notificó por correo." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error enviar flash report {Id}", id); return StatusCode(500, new { message = "Error del servidor." }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        try
        {
            await _service.EliminarAsync(id);
            return Ok(new { message = "Flash Report eliminado." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error eliminar flash report {Id}", id); return StatusCode(500, new { message = "Error del servidor." }); }
    }

    private int? ObtenerUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}
