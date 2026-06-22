using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.CharlasFeature.Presentation;

[ApiController]
[Route("api/v1/ssoma-charlas")]
[AllowAnonymous]
public class CharlaController : ControllerBase
{
    private readonly ICharlaService _svc;

    public CharlaController(ICharlaService svc) => _svc = svc;

    // ── Project detection ─────────────────────────────────────────────────────

    [HttpGet("mi-proyecto")]
    public async Task<IActionResult> GetMiProyecto([FromQuery] int userId)
    {
        try
        {
            var result = await _svc.GetMiProyectoAsync(userId);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al obtener el proyecto." }); }
    }

    [HttpGet("proyectos")]
    public async Task<IActionResult> GetProyectos()
    {
        try
        {
            var result = await _svc.GetTodosProyectosAsync();
            return Ok(result);
        }
        catch { return StatusCode(500, new { message = "Error al obtener proyectos." }); }
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen([FromQuery] int proyectoId, [FromQuery] int mes, [FromQuery] int anio)
    {
        try
        {
            var result = await _svc.GetResumenAsync(proyectoId, mes, anio);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al obtener resumen." }); }
    }

    // ── Staff ─────────────────────────────────────────────────────────────────

    [HttpGet("staff")]
    public async Task<IActionResult> GetStaff([FromQuery] int proyectoId)
    {
        try
        {
            var result = await _svc.GetStaffProyectoAsync(proyectoId);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al obtener staff." }); }
    }

    // ── Tab 1: Asistencia ─────────────────────────────────────────────────────

    [HttpGet("charlas")]
    public async Task<IActionResult> GetCharlas([FromQuery] int proyectoId, [FromQuery] int mes, [FromQuery] int anio)
    {
        try
        {
            var result = await _svc.GetCharlasAsync(proyectoId, mes, anio);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al obtener charlas." }); }
    }

    [HttpPost("charlas")]
    public async Task<IActionResult> CrearCharla([FromQuery] int userId, [FromBody] CrearCharlaDto dto)
    {
        try
        {
            var result = await _svc.CrearCharlaAsync(dto, userId);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al crear charla." }); }
    }

    [HttpDelete("charlas/{id}")]
    public async Task<IActionResult> EliminarCharla(int id)
    {
        try
        {
            await _svc.EliminarCharlaAsync(id);
            return NoContent();
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al eliminar charla." }); }
    }

    [HttpGet("charlas/{charlaId}/asistencia")]
    public async Task<IActionResult> GetAsistencia(int charlaId)
    {
        try
        {
            var result = await _svc.GetAsistenciaAsync(charlaId);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al obtener asistencia." }); }
    }

    [HttpPost("charlas/{charlaId}/asistencia")]
    public async Task<IActionResult> GuardarAsistencia(int charlaId, [FromQuery] int userId, [FromBody] GuardarAsistenciaDto dto)
    {
        try
        {
            await _svc.GuardarAsistenciaAsync(charlaId, dto, userId);
            return NoContent();
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al guardar asistencia." }); }
    }

    // ── Tab 2: Capacitaciones Staff ───────────────────────────────────────────

    [HttpGet("capacitaciones")]
    public async Task<IActionResult> GetCapacitaciones([FromQuery] int proyectoId, [FromQuery] int mes, [FromQuery] int anio)
    {
        try
        {
            var result = await _svc.GetCapacitacionesAsync(proyectoId, mes, anio);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al obtener capacitaciones." }); }
    }

    [HttpPost("capacitaciones/{workerId}")]
    public async Task<IActionResult> SubirCapacitacion(int workerId, [FromQuery] int userId, [FromForm] DateTime fecha, [FromForm] string tema, IFormFile file)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _svc.SubirCapacitacionAsync(workerId, fecha, tema, stream, file.FileName, userId);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al subir capacitación." }); }
    }

    [HttpPut("capacitaciones/{id}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromQuery] int userId, [FromBody] CambiarEstadoDto dto)
    {
        try
        {
            var result = await _svc.CambiarEstadoAsync(id, dto.Estado, userId);
            return Ok(result);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al cambiar estado." }); }
    }

    [HttpDelete("capacitaciones/{id}")]
    public async Task<IActionResult> EliminarCapacitacion(int id)
    {
        try
        {
            await _svc.EliminarCapacitacionAsync(id);
            return NoContent();
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch { return StatusCode(500, new { message = "Error al eliminar capacitación." }); }
    }
}
