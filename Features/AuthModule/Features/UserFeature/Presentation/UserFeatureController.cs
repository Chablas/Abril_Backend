using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Dtos;
using Abril_Backend.Features.AuthModule.UserFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.AuthModule.UserFeature.Presentation
{
    [ApiController]
    [Route("api/v1/user-feature")]
    public class UserFeatureController : ControllerBase
    {
        private readonly IUserFeatureService _service;

        public UserFeatureController(IUserFeatureService service)
        {
            _service = service;
        }

        [Authorize]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            try
            {
                if (User.FindFirst(ClaimTypes.NameIdentifier) == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _service.GetPaged(page, pageSize, search);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UserFeatureCreateDto dto)
        {
            try
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (claim == null) return Unauthorized(new { message = "Inicie sesión" });

                dto.CreatedUserId = int.Parse(claim.Value);
                await _service.Create(dto);
                return Ok(new { message = "Usuario creado y correo enviado." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UserFeatureUpdateDto dto)
        {
            try
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (claim == null) return Unauthorized(new { message = "Inicie sesión" });

                var updatedUserId = int.Parse(claim.Value);
                await _service.Update(id, dto, updatedUserId);
                return Ok(new { message = "Usuario actualizado correctamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPatch("{id:int}/toggle")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            try
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (claim == null) return Unauthorized(new { message = "Inicie sesión" });

                var updatedUserId = int.Parse(claim.Value);
                await _service.ToggleActive(id, updatedUserId);
                return Ok(new { message = "Estado del usuario actualizado." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (claim == null) return Unauthorized(new { message = "Inicie sesión" });

                var updatedUserId = int.Parse(claim.Value);
                await _service.Delete(id, updatedUserId);
                return Ok(new { message = "Usuario eliminado correctamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
