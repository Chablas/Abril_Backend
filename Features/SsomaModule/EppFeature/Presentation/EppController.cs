using System.Security.Claims;
using Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.EppFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.SsomaModule.EppFeature.Presentation
{
    // Catálogo Autorizado de EPP — visible tanto para SSOMA (crea/edita el catálogo técnico)
    // como para Logística (mismo permiso, ver [feature] ssoma.gestion.epp), con bitácora de
    // quién y cuándo modificó cada ítem/modelo.
    [ApiController]
    [Route("api/v1/ssoma/epp")]
    [Authorize]
    [RequireFeature("ssoma.gestion.epp")]
    public class EppController : ControllerBase
    {
        private readonly IEppService _service;

        public EppController(IEppService service)
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
        public async Task<IActionResult> CreateCategoria([FromBody] EppCategoriaUpsertDto dto)
        {
            try { return Ok(await _service.CreateCategoriaAsync(dto)); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPut("categorias/{categoriaId:int}")]
        public async Task<IActionResult> UpdateCategoria(int categoriaId, [FromBody] EppCategoriaUpsertDto dto)
        {
            try { await _service.UpdateCategoriaAsync(categoriaId, dto); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPatch("categorias/{categoriaId:int}/activo")]
        public async Task<IActionResult> SetCategoriaActivo(int categoriaId, [FromQuery] bool activo)
        {
            try { await _service.SetCategoriaActivoAsync(categoriaId, activo); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Elimina la categoría por completo, solo si no tiene familias registradas (ej. registro de prueba).</summary>
        [HttpDelete("categorias/{categoriaId:int}")]
        public async Task<IActionResult> DeleteCategoria(int categoriaId)
        {
            try { await _service.DeleteCategoriaAsync(categoriaId); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        // ─────────────────────────────────────────────────────────────────
        // FAMILIAS
        // ─────────────────────────────────────────────────────────────────

        [HttpGet("familias")]
        public async Task<IActionResult> GetFamilias()
        {
            try { return Ok(await _service.GetFamiliasAsync()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("familias")]
        public async Task<IActionResult> CreateFamilia([FromBody] EppFamiliaUpsertDto dto)
        {
            try { return Ok(await _service.CreateFamiliaAsync(dto)); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPut("familias/{familiaId:int}")]
        public async Task<IActionResult> UpdateFamilia(int familiaId, [FromBody] EppFamiliaUpsertDto dto)
        {
            try { await _service.UpdateFamiliaAsync(familiaId, dto); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPatch("familias/{familiaId:int}/activo")]
        public async Task<IActionResult> SetFamiliaActivo(int familiaId, [FromQuery] bool activo)
        {
            try { await _service.SetFamiliaActivoAsync(familiaId, activo); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Elimina la familia por completo, solo si no tiene ítems registrados (ej. registro de prueba).</summary>
        [HttpDelete("familias/{familiaId:int}")]
        public async Task<IActionResult> DeleteFamilia(int familiaId)
        {
            try { await _service.DeleteFamiliaAsync(familiaId); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        // ─────────────────────────────────────────────────────────────────
        // ÍTEMS
        // ─────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetItems()
        {
            try { return Ok(await _service.GetItemsAsync()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpGet("{itemId:int}")]
        public async Task<IActionResult> GetItemDetalle(int itemId)
        {
            try
            {
                var result = await _service.GetItemDetalleAsync(itemId);
                if (result == null) return NotFound(new { message = "Ítem de EPP no encontrado." });
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost]
        public async Task<IActionResult> CreateItem([FromBody] EppItemUpsertDto dto)
        {
            try
            {
                var result = await _service.CreateItemAsync(dto, GetUserId());
                return CreatedAtAction(nameof(GetItemDetalle), new { itemId = result.Id }, result);
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPut("{itemId:int}")]
        public async Task<IActionResult> UpdateItem(int itemId, [FromBody] EppItemUpsertDto dto)
        {
            try { await _service.UpdateItemAsync(itemId, dto, GetUserId()); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPatch("{itemId:int}/activo")]
        public async Task<IActionResult> SetItemActivo(int itemId, [FromQuery] bool activo)
        {
            try { await _service.SetItemActivoAsync(itemId, activo, GetUserId()); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("{itemId:int}/imagen")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> SubirImagenItem(int itemId, IFormFile archivo)
        {
            try { return Ok(new { imagenUrl = await _service.SubirImagenItemAsync(itemId, archivo, GetUserId()) }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("{itemId:int}/ficha-tecnica")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> SubirFichaTecnica(int itemId, IFormFile archivo)
        {
            try { return Ok(new { fichaTecnicaUrl = await _service.SubirFichaTecnicaAsync(itemId, archivo, GetUserId()) }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpDelete("{itemId:int}/ficha-tecnica")]
        public async Task<IActionResult> QuitarFichaTecnica(int itemId)
        {
            try { await _service.QuitarFichaTecnicaAsync(itemId, GetUserId()); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        // ─────────────────────────────────────────────────────────────────
        // MODELOS / MARCAS
        // ─────────────────────────────────────────────────────────────────

        [HttpPost("{itemId:int}/modelos")]
        public async Task<IActionResult> CreateModelo(int itemId, [FromBody] EppModeloUpsertDto dto)
        {
            try { return Ok(await _service.CreateModeloAsync(itemId, dto, GetUserId())); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPut("modelos/{modeloId:int}")]
        public async Task<IActionResult> UpdateModelo(int modeloId, [FromBody] EppModeloUpsertDto dto)
        {
            try { await _service.UpdateModeloAsync(modeloId, dto, GetUserId()); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPatch("modelos/{modeloId:int}/activo")]
        public async Task<IActionResult> SetModeloActivo(int modeloId, [FromQuery] bool activo)
        {
            try { await _service.SetModeloActivoAsync(modeloId, activo, GetUserId()); return NoContent(); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        [HttpPost("modelos/{modeloId:int}/imagen")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> SubirImagenModelo(int modeloId, IFormFile archivo)
        {
            try { return Ok(new { imagenUrl = await _service.SubirImagenModeloAsync(modeloId, archivo, GetUserId()) }); }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }
    }
}
