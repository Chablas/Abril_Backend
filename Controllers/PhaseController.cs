using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace Abril_Backend.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class PhaseController : ControllerBase
    {
        PhaseRepository _repository;
        public PhaseController(PhaseRepository repository)
        {
            _repository = repository;
        }

        [Authorize]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });
                if (page < 1)
                    page = 1;
                var result = await _repository.GetPaged(page);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PhaseCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                if (string.IsNullOrWhiteSpace(dto.PhaseDescription))
                    return BadRequest(new { message = "PhaseDescription es obligatorio." });
                var result = await _repository.Create(dto, userId);
                return Ok(new { message = "Fase creada exitosamente" });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPut]
        public async Task<IActionResult> Edit([FromBody] PhaseEditDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                if (string.IsNullOrWhiteSpace(dto.PhaseDescription))
                    return BadRequest(new { message = "PhaseDescription es obligatorio." });
                var result = await _repository.Update(dto, userId);
                return Ok(new { message = "Fase actualizada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
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

                var result = await _repository.DeleteSoftAsync(id, userId);
                if (!result)
                    return NotFound(new { message = "Fase no encontrada." });
                return Ok(new { message = "Fase eliminada exitosamente." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}