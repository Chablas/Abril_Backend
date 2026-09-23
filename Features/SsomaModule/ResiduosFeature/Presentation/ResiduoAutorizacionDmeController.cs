using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Presentation;

/// <summary>Autorizaciones de Disposición de Material Excedente (DME) por obra.</summary>
[ApiController]
[Route("api/v1/ssoma/gestion/residuos/autorizaciones-dme")]
[Authorize]
public class ResiduoAutorizacionDmeController : ControllerBase
{
    private readonly IResiduoAutorizacionDmeService _service;
    public ResiduoAutorizacionDmeController(IResiduoAutorizacionDmeService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? projectId, [FromQuery] string? estado)
    {
        try { return Ok(await _service.ListarAsync(projectId, estado)); }
        catch (Exception) { return StatusCode(500, new { message = "Error al listar las autorizaciones DME." }); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        try
        {
            var entidad = await _service.ObtenerAsync(id);
            if (entidad == null) return NotFound(new { message = "Autorización DME no encontrada." });
            return Ok(entidad);
        }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener la autorización DME." }); }
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Crear([FromForm] ResiduoAutorizacionDmeUpsertDto dto, IFormFile? archivo)
    {
        try
        {
            var id = await _service.CrearAsync(dto, archivo);
            return Ok(new { id });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al crear la autorización DME." }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ResiduoAutorizacionDmeUpsertDto dto)
    {
        try
        {
            await _service.ActualizarAsync(id, dto);
            return Ok(new { message = "Autorización DME actualizada correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al actualizar la autorización DME." }); }
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
    public async Task<IActionResult> Anular(int id)
    {
        try
        {
            await _service.AnularAsync(id);
            return Ok(new { message = "Autorización DME anulada correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al anular la autorización DME." }); }
    }
}
