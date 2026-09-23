using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Presentation;

/// <summary>Declaración anual (DAMRS/SIGERSOL) por razón social (contributor), con detalle mensual
/// por tipo de residuo y tipo de manejo, generado/recalculado automáticamente desde los viajes.</summary>
[ApiController]
[Route("api/v1/ssoma/gestion/residuos/declaraciones")]
[Authorize]
public class ResiduoDeclaracionController : ControllerBase
{
    private readonly IResiduoDeclaracionService _service;
    public ResiduoDeclaracionController(IResiduoDeclaracionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? contributorId, [FromQuery] int? periodoAnio)
    {
        try { return Ok(await _service.ListarAsync(contributorId, periodoAnio)); }
        catch (Exception) { return StatusCode(500, new { message = "Error al listar las declaraciones." }); }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(int id)
    {
        try { return Ok(await _service.ObtenerAsync(id)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener la declaración." }); }
    }

    /// <summary>Genera o recalcula el detalle mensual de la declaración de un contributor+año a partir
    /// de los viajes registrados. No sobrescribe una declaración PRESENTADA salvo forzar=true.</summary>
    [HttpPost("recalcular")]
    public async Task<IActionResult> Recalcular([FromBody] ResiduoDeclaracionRecalcularDto dto)
    {
        try { return Ok(await _service.RecalcularAsync(dto)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al recalcular la declaración." }); }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ResiduoDeclaracionUpsertDto dto)
    {
        try
        {
            var id = await _service.CrearAsync(dto);
            return Ok(new { id });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al crear la declaración." }); }
    }

    [HttpPut("{declaracionId}/detalles")]
    public async Task<IActionResult> UpsertDetalle(int declaracionId, [FromBody] ResiduoDeclaracionDetalleUpsertDto dto)
    {
        try
        {
            var id = await _service.UpsertDetalleAsync(declaracionId, dto);
            return Ok(new { id });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al guardar el detalle." }); }
    }

    [HttpDelete("detalles/{detalleId}")]
    public async Task<IActionResult> EliminarDetalle(int detalleId)
    {
        try { await _service.EliminarDetalleAsync(detalleId); return Ok(new { message = "Detalle eliminado correctamente." }); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al eliminar el detalle." }); }
    }

    [HttpPost("{id}/presentar")]
    public async Task<IActionResult> MarcarPresentada(int id, [FromBody] ResiduoDeclaracionMarcarPresentadaDto dto)
    {
        try { await _service.MarcarPresentadaAsync(id, dto); return Ok(new { message = "Declaración marcada como presentada." }); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al marcar la declaración como presentada." }); }
    }

    [HttpPost("{id}/reabrir")]
    public async Task<IActionResult> VolverABorrador(int id)
    {
        try { await _service.VolverABorradorAsync(id); return Ok(new { message = "Declaración vuelta a borrador." }); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al reabrir la declaración." }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        try { await _service.EliminarAsync(id); return Ok(new { message = "Declaración eliminada correctamente." }); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al eliminar la declaración." }); }
    }

    [HttpPost("{declaracionId}/eo-rs")]
    public async Task<IActionResult> CrearEoRsInterviniente(int declaracionId, [FromBody] ResiduoDeclaracionEoRsUpsertDto dto)
    {
        try
        {
            var id = await _service.CrearEoRsIntervinienteAsync(declaracionId, dto);
            return Ok(new { id });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al agregar el EO-RS interviniente." }); }
    }

    [HttpPut("eo-rs/{itemId}")]
    public async Task<IActionResult> ActualizarEoRsInterviniente(int itemId, [FromBody] ResiduoDeclaracionEoRsUpsertDto dto)
    {
        try { await _service.ActualizarEoRsIntervinienteAsync(itemId, dto); return Ok(new { message = "Registro actualizado correctamente." }); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al actualizar el registro." }); }
    }

    [HttpDelete("eo-rs/{itemId}")]
    public async Task<IActionResult> EliminarEoRsInterviniente(int itemId)
    {
        try { await _service.EliminarEoRsIntervinienteAsync(itemId); return Ok(new { message = "Registro eliminado correctamente." }); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al eliminar el registro." }); }
    }
}
