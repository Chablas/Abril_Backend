using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.InspeccionCruzadaProgramacionFeature.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/inspeccion-cruzada-programacion")]
    [Authorize(Roles = $"{Roles.AdministradorSsoma},{Roles.CoordinadorSsoma},{Roles.Prevencionista}")]
    public class InspeccionCruzadaProgramacionController : ControllerBase
    {
        private readonly IInspeccionCruzadaProgramacionService _service;

        public InspeccionCruzadaProgramacionController(IInspeccionCruzadaProgramacionService service)
        {
            _service = service;
        }

        [HttpGet("proyectos-disponibles")]
        public async Task<IActionResult> GetProyectosDisponibles()
        {
            try { return Ok(await _service.GetProyectosDisponiblesAsync()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("anillos")]
        public async Task<IActionResult> GetAnillos()
        {
            try { return Ok(await _service.GetAnillosAsync()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("anillos")]
        public async Task<IActionResult> CrearAnillo([FromBody] CrearAnilloDto dto)
        {
            try { return Ok(await _service.CrearAnilloAsync(dto.Nombre)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("anillos/{anilloId:int}/miembros")]
        public async Task<IActionResult> AgregarMiembro(int anilloId, [FromBody] AgregarMiembroDto dto)
        {
            try { return Ok(await _service.AgregarMiembroAsync(anilloId, dto.ProyectoId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("miembros/reordenar")]
        public async Task<IActionResult> Reordenar([FromBody] ReordenarDto dto)
        {
            try { await _service.ReordenarAsync(dto); return NoContent(); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("miembros/{id:int}/activo")]
        public async Task<IActionResult> SetActivo(int id, [FromBody] ActivoDto dto)
        {
            try { await _service.SetActivoAsync(id, dto.Activo); return NoContent(); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("calendario")]
        public async Task<IActionResult> GetCalendario(
            [FromQuery] int anioDesde, [FromQuery] int mesDesde, [FromQuery] int anioHasta, [FromQuery] int mesHasta)
        {
            try { return Ok(await _service.GetProgramacionAsync(anioDesde, mesDesde, anioHasta, mesHasta)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("calendario/{id:int}/reasignar")]
        public async Task<IActionResult> Reasignar(int id, [FromBody] ReasignarProgramacionDto dto)
        {
            try { await _service.ReasignarAsync(id, dto); return NoContent(); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
