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

    public AccidenteIncidenteController(IAccidenteIncidenteService service, ILogger<AccidenteIncidenteController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int? proyectoId,
        [FromQuery] string? tipo,
        [FromQuery] string? estado,
        [FromQuery] DateTime? fechaDesde,
        [FromQuery] DateTime? fechaHasta,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try { return Ok(await _service.GetListAsync(proyectoId, tipo, estado, fechaDesde, fechaHasta, page, pageSize)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error lista accidentes-incidentes"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        try { return Ok(await _service.GetDetalleAsync(id)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error detalle accidente-incidente {Id}", id); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearAccidenteIncidenteRequest request)
    {
        try
        {
            if (request.ProyectoId <= 0)
                return BadRequest(new { message = "El proyecto es requerido." });
            if (string.IsNullOrWhiteSpace(request.Descripcion))
                return BadRequest(new { message = "La descripción es requerida." });
            if (string.IsNullOrWhiteSpace(request.Tipo))
                return BadRequest(new { message = "El tipo es requerido." });

            var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? usuarioId = int.TryParse(usuarioIdStr, out var uid) ? uid : null;

            var id = await _service.CrearAsync(request, usuarioId);
            return Ok(new { id, message = "Accidente/Incidente registrado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error crear accidente-incidente"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarAccidenteIncidenteRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Descripcion))
                return BadRequest(new { message = "La descripción es requerida." });
            await _service.ActualizarAsync(id, request);
            return Ok(new { message = "Accidente/Incidente actualizado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error actualizar accidente-incidente {Id}", id); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        try
        {
            await _service.EliminarAsync(id);
            return Ok(new { message = "Accidente/Incidente eliminado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error eliminar accidente-incidente {Id}", id); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("{id:int}/documentos")]
    public async Task<IActionResult> SubirDocumento(int id, [FromBody] SubirDocumentoRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ContenidoBase64))
                return BadRequest(new { message = "El contenido del archivo es requerido." });
            var usuarioIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? usuarioId = int.TryParse(usuarioIdStr, out var uid) ? uid : null;
            var docId = await _service.SubirDocumentoAsync(id, request, usuarioId);
            return Ok(new { id = docId, message = "Documento subido correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error subir documento accidente {Id}", id); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}/documentos/{docId:int}")]
    public async Task<IActionResult> GetDocumento(int id, int docId)
    {
        try { return Ok(await _service.GetDocumentoAsync(id, docId)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error obtener documento {DocId}", docId); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }
}
