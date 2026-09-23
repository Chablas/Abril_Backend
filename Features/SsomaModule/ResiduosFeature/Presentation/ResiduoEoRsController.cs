using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Presentation;

/// <summary>Catálogo de EO-RS / transportistas / escombreras / plantas de valorización, y su
/// checklist documentario.</summary>
[ApiController]
[Route("api/v1/ssoma/gestion/residuos/eo-rs")]
[Authorize]
public class ResiduoEoRsController : ControllerBase
{
    private readonly IResiduoEoRsService _service;
    public ResiduoEoRsController(IResiduoEoRsService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool? activo, [FromQuery] string? tipoOperador)
    {
        try { return Ok(await _service.ListarAsync(activo, tipoOperador)); }
        catch (Exception) { return StatusCode(500, new { message = "Error al listar los EO-RS." }); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        try
        {
            var entidad = await _service.ObtenerAsync(id);
            if (entidad == null) return NotFound(new { message = "EO-RS no encontrado." });
            return Ok(entidad);
        }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener el EO-RS." }); }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ResiduoEoRsUpsertDto dto)
    {
        try
        {
            var id = await _service.CrearAsync(dto);
            return Ok(new { id });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al crear el EO-RS." }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ResiduoEoRsUpsertDto dto)
    {
        try
        {
            await _service.ActualizarAsync(id, dto);
            return Ok(new { message = "EO-RS actualizado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al actualizar el EO-RS." }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Desactivar(int id)
    {
        try
        {
            await _service.DesactivarAsync(id);
            return Ok(new { message = "EO-RS desactivado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al desactivar el EO-RS." }); }
    }

    [HttpGet("{id}/documentos")]
    public async Task<IActionResult> ListarDocumentos(int id)
    {
        try { return Ok(await _service.ListarDocumentosAsync(id)); }
        catch (Exception) { return StatusCode(500, new { message = "Error al listar los documentos." }); }
    }

    [HttpPost("{id}/documentos")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> CrearDocumento(int id, [FromForm] ResiduoEoRsDocumentoUpsertDto dto, IFormFile? archivo)
    {
        try
        {
            var docId = await _service.CrearDocumentoAsync(id, dto, archivo);
            return Ok(new { id = docId });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al crear el documento." }); }
    }

    [HttpPut("documentos/{documentoId}")]
    public async Task<IActionResult> ActualizarDocumento(int documentoId, [FromBody] ResiduoEoRsDocumentoUpsertDto dto)
    {
        try
        {
            await _service.ActualizarDocumentoAsync(documentoId, dto);
            return Ok(new { message = "Documento actualizado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al actualizar el documento." }); }
    }

    [HttpPost("documentos/{documentoId}/archivo")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> SubirArchivoDocumento(int documentoId, IFormFile archivo)
    {
        try
        {
            var url = await _service.SubirArchivoDocumentoAsync(documentoId, archivo);
            return Ok(new { url });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al subir el archivo." }); }
    }

    [HttpDelete("documentos/{documentoId}")]
    public async Task<IActionResult> EliminarDocumento(int documentoId)
    {
        try
        {
            await _service.EliminarDocumentoAsync(documentoId);
            return Ok(new { message = "Documento eliminado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al eliminar el documento." }); }
    }
}
