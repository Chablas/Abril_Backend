using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Presentation;

/// <summary>Repositorio de plantillas/manuales de referencia (manuales SIGERSOL, modelo de
/// declaración jurada, modelo de características de residuos sólidos).</summary>
[ApiController]
[Route("api/v1/ssoma/gestion/residuos/documentos-referencia")]
[Authorize]
public class ResiduoDocumentoReferenciaController : ControllerBase
{
    private readonly IResiduoDocumentoReferenciaService _service;
    public ResiduoDocumentoReferenciaController(IResiduoDocumentoReferenciaService service) => _service = service;

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? tipo, [FromQuery] bool? activo)
    {
        try { return Ok(await _service.ListarAsync(tipo, activo)); }
        catch (Exception) { return StatusCode(500, new { message = "Error al listar los documentos de referencia." }); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        try
        {
            var entidad = await _service.ObtenerAsync(id);
            if (entidad == null) return NotFound(new { message = "Documento de referencia no encontrado." });
            return Ok(entidad);
        }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener el documento de referencia." }); }
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Crear([FromForm] ResiduoDocumentoReferenciaUpsertDto dto, IFormFile archivo)
    {
        try
        {
            var id = await _service.CrearAsync(dto, archivo, GetUserId());
            return Ok(new { id });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al crear el documento de referencia." }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ResiduoDocumentoReferenciaUpsertDto dto)
    {
        try
        {
            await _service.ActualizarAsync(id, dto);
            return Ok(new { message = "Documento de referencia actualizado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al actualizar el documento de referencia." }); }
    }

    [HttpPost("{id}/archivo")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> SubirArchivo(int id, IFormFile archivo)
    {
        try
        {
            var url = await _service.SubirArchivoAsync(id, archivo);
            return Ok(new { url });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al subir el archivo." }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Desactivar(int id)
    {
        try
        {
            await _service.DesactivarAsync(id);
            return Ok(new { message = "Documento de referencia desactivado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al desactivar el documento de referencia." }); }
    }
}
