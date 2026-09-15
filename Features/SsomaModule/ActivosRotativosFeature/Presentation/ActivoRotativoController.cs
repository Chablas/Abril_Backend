using System.Security.Claims;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/activos-rotativos")]
    [Authorize]
    [RequireFeature("ssoma.gestion.activos-rotativos")]
    public class ActivoRotativoController : ControllerBase
    {
        private readonly IActivoRotativoService _service;

        // Eliminar un activo es irreversible (no queda historial, a diferencia de "dar de
        // baja"), así que se restringe a un único correo, mismo criterio que
        // EmailAutorizadoParaBorrar en TrabajadorRestringidoController / EmailAutorizadoParaEditar
        // en AmonestacionController — cuando haya más de una persona con este acceso, esto
        // debería pasar a un rol/permiso propio en vez de un email hardcodeado.
        private const string EmailAutorizadoParaEliminar = "sjustiniani@abril.pe";

        private bool PuedeEliminar() =>
            string.Equals(User.FindFirst(ClaimTypes.Email)?.Value, EmailAutorizadoParaEliminar, StringComparison.OrdinalIgnoreCase);

        public ActivoRotativoController(IActivoRotativoService service)
        {
            _service = service;
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
        }

        // ─────────────────────────────────────────────────────────────────
        // MATERIALES (catálogo único)
        // ─────────────────────────────────────────────────────────────────

        [HttpGet("materiales")]
        public async Task<IActionResult> GetMateriales()
        {
            try { return Ok(await _service.GetMaterialesAsync()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("materiales")]
        public async Task<IActionResult> CreateMaterial([FromBody] ActivoRotativoMaterialUpsertDto dto)
        {
            try { return Ok(await _service.CreateMaterialAsync(dto)); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPut("materiales/{materialId:int}")]
        public async Task<IActionResult> UpdateMaterial(int materialId, [FromBody] ActivoRotativoMaterialUpsertDto dto)
        {
            try
            {
                await _service.UpdateMaterialAsync(materialId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Elimina un material del catálogo, solo si no tiene activos registrados.</summary>
        [HttpDelete("materiales/{materialId:int}")]
        public async Task<IActionResult> DeleteMaterial(int materialId)
        {
            try
            {
                await _service.DeleteMaterialAsync(materialId);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Coordinadores SSOMA y Prevencionistas de Abril (staff propio), para el selector de "Responsable/contacto".</summary>
        [HttpGet("responsables-ssoma")]
        public async Task<IActionResult> GetResponsablesSsoma()
        {
            try { return Ok(await _service.GetResponsablesSsomaAsync()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Busca ítems del catálogo de Presupuesto Materiales (S10) para vincular un Material. No requiere el feature de Presupuesto.</summary>
        [HttpGet("materiales/buscar-item-presupuesto")]
        public async Task<IActionResult> BuscarItemPresupuesto([FromQuery] string? q)
        {
            try { return Ok(await _service.BuscarItemsPresupuestoAsync(q ?? "")); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        // ─────────────────────────────────────────────────────────────────
        // ACTIVOS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Lista todos los activos rotativos con su proyecto actual y contacto.</summary>
        [HttpGet]
        public async Task<IActionResult> GetActivos()
        {
            try { return Ok(await _service.GetActivosAsync()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Detalle de un activo con su historial de traspasos entre proyectos.</summary>
        [HttpGet("{activoId:int}")]
        public async Task<IActionResult> GetActivoDetalle(int activoId)
        {
            try
            {
                var result = await _service.GetActivoDetalleAsync(activoId);
                if (result == null) return NotFound(new { message = "Activo no encontrado." });
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost]
        public async Task<IActionResult> CreateActivo([FromBody] ActivoRotativoUpsertDto dto)
        {
            try
            {
                var result = await _service.CreateActivoAsync(dto);
                return CreatedAtAction(nameof(GetActivoDetalle), new { activoId = result.Id }, result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPut("{activoId:int}")]
        public async Task<IActionResult> UpdateActivo(int activoId, [FromBody] ActivoRotativoUpsertDto dto)
        {
            try
            {
                await _service.UpdateActivoAsync(activoId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Elimina el activo por completo (no "dar de baja") — para registros de prueba que nunca debieron contabilizarse. Acceso restringido — ver PuedeEliminar().</summary>
        [HttpDelete("{activoId:int}")]
        public async Task<IActionResult> DeleteActivo(int activoId)
        {
            try
            {
                if (!PuedeEliminar())
                    return StatusCode(403, new { message = "No tiene permisos para eliminar activos." });

                await _service.DeleteActivoAsync(activoId);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Traspasa el activo a otro proyecto (o a almacén central si NuevoProyectoId es null). Queda en el historial.</summary>
        [HttpPost("{activoId:int}/mover")]
        public async Task<IActionResult> MoverActivo(int activoId, [FromBody] ActivoRotativoMoverDto dto)
        {
            try
            {
                var result = await _service.MoverActivoAsync(activoId, dto, GetUserId());
                return Ok(result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }
    }
}
