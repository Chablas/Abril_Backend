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
        // CATEGORÍAS
        // ─────────────────────────────────────────────────────────────────

        [HttpGet("categorias")]
        public async Task<IActionResult> GetCategorias()
        {
            try { return Ok(await _service.GetCategoriasAsync()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("categorias")]
        public async Task<IActionResult> CreateCategoria([FromBody] ActivoRotativoCategoriaUpsertDto dto)
        {
            try { return Ok(await _service.CreateCategoriaAsync(dto)); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPut("categorias/{categoriaId:int}")]
        public async Task<IActionResult> UpdateCategoria(int categoriaId, [FromBody] ActivoRotativoCategoriaUpsertDto dto)
        {
            try
            {
                await _service.UpdateCategoriaAsync(categoriaId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
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
