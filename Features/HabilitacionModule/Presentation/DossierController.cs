using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Dossier;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Habilitacion.Presentation;

[ApiController]
[Route("api/v1/habilitacion/dossier")]
[Authorize]
public class DossierController : ControllerBase
{
    private readonly IDossierService _service;
    private readonly ILogger<DossierController> _logger;

    public DossierController(IDossierService service, ILogger<DossierController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSemanas(
        [FromQuery] int? contributorId,
        [FromQuery] int? proyectoId,
        [FromQuery] int? anio)
    {
        try { return Ok(await _service.GetSemanasAsync(contributorId, proyectoId, anio)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error GetSemanas dossier"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        try { return Ok(await _service.GetDetalleAsync(id)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error GetDetalle dossier {Id}", id); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("semana")]
    public async Task<IActionResult> EnsureSemana([FromBody] EnsureSemanaRequest req)
    {
        try { return Ok(await _service.EnsureSemanaAsync(req)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error EnsureSemana dossier"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("{dossierId:int}/documento")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirDocumento(int dossierId, [FromForm] SubirDocumentoDossierRequest req)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(req.TipoDoc))
                return BadRequest(new { message = "El tipo de documento es requerido." });
            await _service.SubirDocumentoAsync(dossierId, req.TipoDoc, req.File!);
            return Ok(new { message = "Documento subido correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error SubirDocumento dossier {DossierId}", dossierId); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("documento/{docId:int}/marcar-na")]
    public async Task<IActionResult> MarcarNa(int docId)
    {
        try
        {
            await _service.MarcarNaAsync(docId);
            return Ok(new { message = "Estado actualizado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error MarcarNA doc {DocId}", docId); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("{dossierId:int}/enviar")]
    public async Task<IActionResult> Enviar(int dossierId)
    {
        try
        {
            await _service.EnviarAsync(dossierId);
            return Ok(new { message = "Dossier enviado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error Enviar dossier {DossierId}", dossierId); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("{dossierId:int}/revisar")]
    public async Task<IActionResult> Revisar(int dossierId, [FromBody] RevisarDossierRequest req)
    {
        try
        {
            await _service.RevisarAsync(dossierId, req);
            return Ok(new { message = "Dossier revisado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error Revisar dossier {DossierId}", dossierId); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("documento/{docId:int}/revisar")]
    public async Task<IActionResult> RevisarDocumento(int docId, [FromBody] RevisarDocumentoRequest req)
    {
        try
        {
            await _service.RevisarDocumentoAsync(docId, req);
            return Ok(new { message = "Documento revisado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error RevisarDocumento doc {DocId}", docId); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{dossierId:int}/no-aplica")]
    public async Task<IActionResult> MarcarNoAplica(int dossierId)
    {
        try
        {
            await _service.MarcarSemanaNoAplicaAsync(dossierId);
            return Ok(new { message = "Semana marcada como No Aplica." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error MarcarNoAplica dossier {Id}", dossierId); return StatusCode(500, new { message = "Error del servidor." }); }
    }

    [HttpDelete("archivo/{archivoId:int}")]
    public async Task<IActionResult> EliminarArchivo(int archivoId)
    {
        try
        {
            await _service.EliminarArchivoAsync(archivoId);
            return Ok(new { message = "Archivo eliminado." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error EliminarArchivo {ArchivoId}", archivoId); return StatusCode(500, new { message = "Error del servidor." }); }
    }

    [HttpGet("archivo/{archivoId:int}/url")]
    public async Task<IActionResult> GetArchivoUrl(int archivoId)
    {
        try
        {
            var url = await _service.GetArchivoUrlAsync(archivoId);
            return Ok(new { url });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error GetArchivoUrl {ArchivoId}", archivoId); return StatusCode(500, new { message = "Error del servidor." }); }
    }

    [HttpGet("documento/{docId:int}/url")]
    public async Task<IActionResult> GetDocumentoUrl(int docId)
    {
        try
        {
            var url = await _service.GetDocumentoUrlAsync(docId);
            return Ok(new { url });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error GetDocumentoUrl {DocId}", docId); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }
}
