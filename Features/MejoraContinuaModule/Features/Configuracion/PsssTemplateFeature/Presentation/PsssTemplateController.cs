using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Presentation
{
    [ApiController]
    [Route("api/v1/psss-template")]
    public class PsssTemplateController : ControllerBase
    {
        private readonly IPsssTemplateService _service;

        public PsssTemplateController(IPsssTemplateService service)
        {
            _service = service;
        }

        private IActionResult UserRequired(out int userId)
        {
            userId = 0;
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null) return Unauthorized(new { message = "Inicie sesión" });
            userId = int.Parse(claim.Value);
            return null!;
        }

        [Authorize]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1)
        {
            try
            {
                return Ok(await _service.GetPagedAsync(page));
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                return Ok(await _service.GetAllSimpleAsync());
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("all-psss-flat")]
        public async Task<IActionResult> GetAllPsssFlat()
        {
            try
            {
                return Ok(await _service.GetAllPsssFlat());
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PsssTemplateCreateDTO dto)
        {
            try
            {
                var authResult = UserRequired(out var userId);
                if (authResult != null) return authResult;

                if (string.IsNullOrWhiteSpace(dto.TemplateName))
                    return BadRequest(new { message = "El nombre de la plantilla es requerido." });

                var id = await _service.CreateAsync(dto, userId);
                return Ok(new { psssTemplateId = id, message = "Plantilla creada exitosamente." });
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
                var authResult = UserRequired(out var userId);
                if (authResult != null) return authResult;

                var deleted = await _service.DeleteSoftAsync(id, userId);
                if (!deleted) return NotFound(new { message = "Plantilla no encontrada." });

                return Ok(new { message = "Plantilla eliminada exitosamente." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("{id}/psss-ids")]
        public async Task<IActionResult> GetPsssIds(int id)
        {
            try
            {
                return Ok(await _service.GetPsssIdsAsync(id));
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPut("{id}/psss")]
        public async Task<IActionResult> UpdatePsss(int id, [FromBody] UpdateTemplatePsssDTO dto)
        {
            try
            {
                await _service.UpdatePsssAsync(id, dto.PsssIds);
                return Ok(new { message = "Relaciones de plantilla actualizadas." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
