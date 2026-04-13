using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.MicrosoftAuth.MicrosoftLogin.Presentation
{
    [ApiController]
    [Route("api/v1/microsoft")]
    public class MicrosoftLoginController : ControllerBase
    {
        private readonly IMicrosoftLoginService _service;

        public MicrosoftLoginController(IMicrosoftLoginService service)
        {
            _service = service;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login()
        {
            try
            {
                var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                if (string.IsNullOrWhiteSpace(token))
                    return Unauthorized(new { message = "Token de Microsoft requerido." });

                var result = await _service.Login(token);
                return Ok(result);
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
