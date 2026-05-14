using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Presentation
{
    [ApiController]
    [Route("api/v1/mejora-continua/relations")]
    public class RelationsController : ControllerBase
    {
        private readonly IRelationsService _service;

        public RelationsController(IRelationsService service)
        {
            _service = service;
        }

        [Authorize]
        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            try
            {
                return Ok(await _service.GetFiltersAsync());
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int? phaseId = null,
            [FromQuery] int? stageId = null,
            [FromQuery] int? layerId = null,
            [FromQuery] int? subStageId = null,
            [FromQuery] int? subSpecialtyId = null,
            [FromQuery] int? partidaId = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                return Ok(await _service.GetPagedAsync(page, phaseId, stageId, layerId, subStageId, subSpecialtyId, partidaId));
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRelationDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                var result = await _service.CreateAsync(dto, userId);

                if (result == null)
                    return BadRequest(new { message = "La relación ya existe." });

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                var result = await _service.DeleteAsync(id, userId);

                if (!result)
                    return NotFound(new { message = "Relación no encontrada." });

                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
