using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Presentation
{
    /// <summary>
    /// Lo de la cuenta que se hace con sesión (Más → Cambiar contraseña). Va aparte de
    /// ConvivirAuthController porque ese es todo [AllowAnonymous], y a nivel de clase gana sobre
    /// cualquier [Authorize] de un método.
    /// </summary>
    [ApiController]
    [Authorize(Roles = Roles.Propietario)]
    [Route("api/v1/convivir/cuenta")]
    public class ConvivirCuentaController : ControllerBase
    {
        private readonly IConvivirAuthService _service;
        private readonly ILogger<ConvivirCuentaController> _logger;

        public ConvivirCuentaController(IConvivirAuthService service, ILogger<ConvivirCuentaController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpPost("cambiar-contrasena")]
        public async Task<IActionResult> CambiarContrasena(ConvivirCambiarContrasenaDto dto)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                    return Unauthorized(new { message = "Tu sesión venció. Vuelve a ingresar." });

                await _service.CambiarContrasena(userId, dto);
                return Ok(new { message = "Listo. Tu contraseña cambió y cerramos tu sesión en los demás dispositivos." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ConvivirCuentaController.CambiarContrasena");
                return StatusCode(500, new { message = "No pudimos cambiar tu contraseña. Inténtalo de nuevo en unos minutos." });
            }
        }
    }
}
