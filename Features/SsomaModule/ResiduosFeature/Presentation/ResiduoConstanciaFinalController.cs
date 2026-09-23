using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Presentation;

/// <summary>Constancia final de obra (cierre de proyecto o de periodo declarado).</summary>
[ApiController]
[Route("api/v1/ssoma/gestion/residuos/constancias-finales")]
[Authorize]
public class ResiduoConstanciaFinalController : ControllerBase
{
    private readonly IResiduoConstanciaFinalService _service;
    public ResiduoConstanciaFinalController(IResiduoConstanciaFinalService service) => _service = service;

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? projectId)
    {
        try { return Ok(await _service.ListarAsync(projectId)); }
        catch (Exception) { return StatusCode(500, new { message = "Error al listar las constancias finales." }); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        try
        {
            var entidad = await _service.ObtenerAsync(id);
            if (entidad == null) return NotFound(new { message = "Constancia final no encontrada." });
            return Ok(entidad);
        }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener la constancia final." }); }
    }

    [HttpPost]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Crear([FromForm] ResiduoConstanciaFinalUpsertDto dto, IFormFile archivo)
    {
        try
        {
            var id = await _service.CrearAsync(dto, archivo, GetUserId());
            return Ok(new { id });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al crear la constancia final." }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ResiduoConstanciaFinalUpsertDto dto)
    {
        try
        {
            await _service.ActualizarAsync(id, dto);
            return Ok(new { message = "Constancia final actualizada correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al actualizar la constancia final." }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        try
        {
            await _service.EliminarAsync(id);
            return Ok(new { message = "Constancia final eliminada correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al eliminar la constancia final." }); }
    }
}
