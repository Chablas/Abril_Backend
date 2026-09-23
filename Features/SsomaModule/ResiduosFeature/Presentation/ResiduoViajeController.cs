using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Presentation;

/// <summary>Registro operativo de viajes/retiros de residuos por obra (GP-FOR-035). El cálculo de
/// toneladas es automático a partir del factor vigente del tipo de residuo, salvo ajuste manual.</summary>
[ApiController]
[Route("api/v1/ssoma/gestion/residuos/viajes")]
[Authorize]
public class ResiduoViajeController : ControllerBase
{
    private readonly IResiduoViajeService _service;
    public ResiduoViajeController(IResiduoViajeService service) => _service = service;

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] ResiduoViajeListFiltroDto filtro)
    {
        try { return Ok(await _service.ListarAsync(filtro)); }
        catch (Exception) { return StatusCode(500, new { message = "Error al listar los viajes." }); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(long id)
    {
        try
        {
            var entidad = await _service.ObtenerAsync(id);
            if (entidad == null) return NotFound(new { message = "Viaje no encontrado." });
            return Ok(entidad);
        }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener el viaje." }); }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ResiduoViajeUpsertDto dto)
    {
        try
        {
            var id = await _service.CrearAsync(dto, GetUserId());
            return Ok(new { id });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al crear el viaje." }); }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(long id, [FromBody] ResiduoViajeUpsertDto dto)
    {
        try
        {
            await _service.ActualizarAsync(id, dto);
            return Ok(new { message = "Viaje actualizado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al actualizar el viaje." }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Desactivar(long id)
    {
        try
        {
            await _service.DesactivarAsync(id);
            return Ok(new { message = "Viaje desactivado correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al desactivar el viaje." }); }
    }

    [HttpPost("{id}/archivo")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> SubirArchivo(long id, IFormFile archivo)
    {
        try
        {
            var url = await _service.SubirArchivoAsync(id, archivo);
            return Ok(new { url });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al subir el archivo." }); }
    }
}
