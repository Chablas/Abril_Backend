using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AuthModule.Role.Application.Dtos;
using Abril_Backend.Features.AuthModule.Role.Application.Interfaces;

namespace Abril_Backend.Features.AuthModule.Role.Presentation
{
    [ApiController]
    [Route("api/v1/role")]
    [Authorize]
    public class RoleFeatureController : ControllerBase
    {
        private readonly IRoleFeatureService _service;

        public RoleFeatureController(IRoleFeatureService service)
        {
            _service = service;
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _service.GetPaged(page, pageSize);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RoleCreateDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                if (string.IsNullOrWhiteSpace(dto.RoleDescription))
                    return BadRequest(new { message = "La descripción del rol es obligatoria." });

                var userId = int.Parse(userIdClaim.Value);
                await _service.Create(dto, userId);
                return Ok(new { message = "Rol creado exitosamente." });
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
    }
}
