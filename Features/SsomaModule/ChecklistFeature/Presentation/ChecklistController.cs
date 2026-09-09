using System.Security.Claims;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ChecklistFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.SsomaModule.ChecklistFeature.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/checklist")]
    [Authorize]
    [RequireFeature("ssoma.gestion.checklist")]
    public class ChecklistController : ControllerBase
    {
        private readonly IChecklistService _service;

        public ChecklistController(IChecklistService service)
        {
            _service = service;
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                ?? throw new UnauthorizedAccessException("Sesión inválida.");
            return int.Parse(claim.Value);
        }

        /// <summary>Proyecto actual del usuario logueado (resuelto vía su Worker), para preseleccionar en "Por Proyecto".</summary>
        [HttpGet("mi-proyecto-actual")]
        public async Task<IActionResult> GetMiProyectoActual()
        {
            try
            {
                var proyectoId = await _service.GetProyectoActualDeUsuarioAsync(GetUserId());
                return Ok(new { proyectoId });
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        // ─────────────────────────────────────────────────────────────────
        // PLANTILLAS (catálogo maestro)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Lista todas las plantillas de checklist.</summary>
        [HttpGet("plantillas")]
        public async Task<IActionResult> GetPlantillas()
        {
            try
            {
                var result = await _service.GetPlantillasAsync();
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Detalle de una plantilla con sus items.</summary>
        [HttpGet("plantillas/{plantillaId:int}")]
        public async Task<IActionResult> GetPlantillaDetalle(int plantillaId)
        {
            try
            {
                var result = await _service.GetPlantillaDetalleAsync(plantillaId);
                if (result == null) return NotFound(new { message = "Plantilla no encontrada." });
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Crea una nueva plantilla de checklist.</summary>
        [HttpPost("plantillas")]
        public async Task<IActionResult> CreatePlantilla([FromBody] ChecklistPlantillaUpsertDto dto)
        {
            try
            {
                var userId = GetUserId();
                var result = await _service.CreatePlantillaAsync(dto, userId);
                return CreatedAtAction(nameof(GetPlantillaDetalle), new { plantillaId = result.Id }, result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Edita una plantilla existente (nombre, descripción, configuración).</summary>
        [HttpPut("plantillas/{plantillaId:int}")]
        public async Task<IActionResult> UpdatePlantilla(int plantillaId, [FromBody] ChecklistPlantillaUpsertDto dto)
        {
            try
            {
                await _service.UpdatePlantillaAsync(plantillaId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Agrega un nuevo item a una plantilla (se propaga automáticamente a proyectos activos).</summary>
        [HttpPost("plantillas/{plantillaId:int}/items")]
        public async Task<IActionResult> AddItemToPlantilla(int plantillaId, [FromBody] ChecklistPlantillaItemCreateDto dto)
        {
            try
            {
                var result = await _service.AddItemToPlantillaAsync(plantillaId, dto);
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Edita un item de plantilla (descripción, adjunto, activo/inactivo).</summary>
        [HttpPut("plantillas/items/{itemId:int}")]
        public async Task<IActionResult> UpdatePlantillaItem(int itemId, [FromBody] ChecklistPlantillaItemEditDto dto)
        {
            try
            {
                await _service.UpdatePlantillaItemAsync(itemId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        // ─────────────────────────────────────────────────────────────────
        // PARTIDAS (etapas constructivas: Muro Anclado, Excavación, etc.)
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Lista todas las partidas.</summary>
        [HttpGet("partidas")]
        public async Task<IActionResult> GetPartidas()
        {
            try
            {
                var result = await _service.GetPartidasAsync();
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Crea una nueva partida.</summary>
        [HttpPost("partidas")]
        public async Task<IActionResult> CreatePartida([FromBody] ChecklistPartidaUpsertDto dto)
        {
            try
            {
                var result = await _service.CreatePartidaAsync(dto, GetUserId());
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Edita una partida existente.</summary>
        [HttpPut("partidas/{partidaId:int}")]
        public async Task<IActionResult> UpdatePartida(int partidaId, [FromBody] ChecklistPartidaUpsertDto dto)
        {
            try
            {
                await _service.UpdatePartidaAsync(partidaId, dto);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Elimina una partida y su(s) plantilla(s), solo si ningún proyecto tiene ya ítems completados en ellas.</summary>
        [HttpDelete("partidas/{partidaId:int}")]
        public async Task<IActionResult> DeletePartida(int partidaId)
        {
            try
            {
                await _service.DeletePartidaAsync(partidaId);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Cambia el orden de un ítem de plantilla (para reflejar la secuencia real de avance de obra).</summary>
        [HttpPatch("plantillas/items/{itemId:int}/orden")]
        public async Task<IActionResult> SetOrdenItem(int itemId, [FromBody] ChecklistItemOrdenDto dto)
        {
            try
            {
                await _service.SetOrdenItemAsync(itemId, dto.Orden);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        // ─────────────────────────────────────────────────────────────────
        // IMÁGENES DE REFERENCIA DE UN ITEM DE PLANTILLA
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Sube una foto de referencia ("cómo debe quedar") a un item. Sin límite de cantidad.</summary>
        [HttpPost("plantillas/items/{itemId:int}/imagenes")]
        public async Task<IActionResult> SubirImagenReferencia(int itemId, [FromForm] IFormFile file)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var result = await _service.SubirImagenReferenciaAsync(itemId, stream, file.FileName);
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Elimina una foto de referencia.</summary>
        [HttpDelete("plantillas/items/imagenes/{imagenId:int}")]
        public async Task<IActionResult> EliminarImagenReferencia(int imagenId)
        {
            try
            {
                await _service.EliminarImagenReferenciaAsync(imagenId);
                return NoContent();
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        // ─────────────────────────────────────────────────────────────────
        // CHECKLISTS DE PROYECTO
        // ─────────────────────────────────────────────────────────────────

        /// <summary>Resumen de todos los checklists de un proyecto (para indicadores/dashboard).</summary>
        [HttpGet("proyecto/{proyectoId:int}/resumen")]
        public async Task<IActionResult> GetResumenProyecto(int proyectoId)
        {
            try
            {
                var result = await _service.GetResumenProyectoAsync(proyectoId);
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Detalle completo de un checklist de proyecto con el estado de cada item.</summary>
        [HttpGet("{checklistProyectoId:int}")]
        public async Task<IActionResult> GetChecklistDetalle(int checklistProyectoId)
        {
            try
            {
                var result = await _service.GetChecklistDetalleAsync(checklistProyectoId);
                if (result == null) return NotFound(new { message = "Checklist no encontrado." });
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Activa manualmente un checklist en un proyecto (Torre Grúa, SUNAFIL, etc.).</summary>
        [HttpPost("proyecto/{proyectoId:int}/activar")]
        public async Task<IActionResult> ActivarChecklist(int proyectoId, [FromBody] ChecklistActivarDto dto)
        {
            try
            {
                var userId = GetUserId();
                var result = await _service.ActivarChecklistAsync(proyectoId, dto.PlantillaId, userId);
                return Ok(result);
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Desactiva un checklist opcional de proyecto, solo si aún no tiene ítems completados.</summary>
        [HttpDelete("{checklistProyectoId:int}")]
        public async Task<IActionResult> DesactivarChecklist(int checklistProyectoId)
        {
            try
            {
                await _service.DesactivarChecklistAsync(checklistProyectoId);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Marca un checklist como "no aplica" (proyecto avanzado que ya pasó esa etapa, u obligatorio que no le corresponde). Requiere motivo.</summary>
        [HttpPost("{checklistProyectoId:int}/no-aplica")]
        public async Task<IActionResult> MarcarNoAplica(int checklistProyectoId, [FromBody] ChecklistNoAplicaDto dto)
        {
            try
            {
                await _service.MarcarNoAplicaAsync(checklistProyectoId, dto.Motivo, GetUserId());
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Revierte el "no aplica" — vuelve a pendiente/en_progreso/completado según sus ítems.</summary>
        [HttpPost("{checklistProyectoId:int}/reactivar")]
        public async Task<IActionResult> ReactivarChecklist(int checklistProyectoId)
        {
            try
            {
                await _service.ReactivarChecklistAsync(checklistProyectoId);
                return NoContent();
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>Sube la evidencia de cumplimiento de un ítem (foto opcional). Devuelve la URL para mandarla junto con el toggle.</summary>
        [HttpPost("items/adjunto")]
        public async Task<IActionResult> SubirAdjuntoItem([FromForm] IFormFile file)
        {
            try
            {
                using var stream = file.OpenReadStream();
                var url = await _service.SubirAdjuntoItemAsync(stream, file.FileName);
                return Ok(new { url });
            }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }

        /// <summary>
        /// Marca o desmarca un item como completado.
        /// Responde con el nuevo porcentaje y estado del checklist padre.
        /// Si el checklist llega a 100%, dispara automáticamente el email al Gerente.
        /// </summary>
        [HttpPatch("items/{checklistProyectoItemId:int}")]
        public async Task<IActionResult> ToggleItem(int checklistProyectoItemId, [FromBody] ChecklistItemToggleDto dto)
        {
            try
            {
                var userId = GetUserId();
                var (porcentaje, estado) = await _service.ToggleItemAsync(checklistProyectoItemId, dto, userId);
                return Ok(new { porcentaje, estado });
            }
            catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor." }); }
        }
    }
}
