using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Presentation;

/// <summary>Catálogo de tipos de residuo y sus factores de conversión m3 -> t.</summary>
[ApiController]
[Route("api/v1/ssoma/gestion/residuos/tipos")]
[Authorize]
public class ResiduoTipoController : ControllerBase
{
    private readonly IResiduoTipoService _service;
    public ResiduoTipoController(IResiduoTipoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool? activo)
    {
        try { return Ok(await _service.ListarAsync(activo)); }
        catch (Exception) { return StatusCode(500, new { message = "Error al listar los tipos de residuo." }); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        try
        {
            var entidad = await _service.ObtenerAsync(id);
            if (entidad == null) return NotFound(new { message = "Tipo de residuo no encontrado." });
            return Ok(entidad);
        }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener el tipo de residuo." }); }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ResiduoTipoUpsertDto dto)
    {
        try
        {
            var id = await _service.CrearAsync(dto);
            return Ok(new { id });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al crear el tipo de residuo." }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ResiduoTipoUpsertDto dto)
    {
        try
        {
            await _service.ActualizarAsync(id, dto);
            return Ok(new { message = "Tipo de residuo actualizado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al actualizar el tipo de residuo." }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Desactivar(int id)
    {
        try
        {
            await _service.DesactivarAsync(id);
            return Ok(new { message = "Tipo de residuo desactivado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al desactivar el tipo de residuo." }); }
    }

    [HttpGet("{id}/factores")]
    public async Task<IActionResult> ListarFactores(int id)
    {
        try { return Ok(await _service.ListarFactoresAsync(id)); }
        catch (Exception) { return StatusCode(500, new { message = "Error al listar los factores." }); }
    }

    [HttpPost("{id}/factores")]
    public async Task<IActionResult> CrearFactor(int id, [FromBody] ResiduoTipoFactorUpsertDto dto)
    {
        try
        {
            var factorId = await _service.CrearFactorAsync(id, dto);
            return Ok(new { id = factorId });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al crear el factor." }); }
    }

    [HttpPut("factores/{factorId}")]
    public async Task<IActionResult> ActualizarFactor(int factorId, [FromBody] ResiduoTipoFactorUpsertDto dto)
    {
        try
        {
            await _service.ActualizarFactorAsync(factorId, dto);
            return Ok(new { message = "Factor actualizado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al actualizar el factor." }); }
    }

    [HttpDelete("factores/{factorId}")]
    public async Task<IActionResult> EliminarFactor(int factorId)
    {
        try
        {
            await _service.EliminarFactorAsync(factorId);
            return Ok(new { message = "Factor eliminado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al eliminar el factor." }); }
    }
}
