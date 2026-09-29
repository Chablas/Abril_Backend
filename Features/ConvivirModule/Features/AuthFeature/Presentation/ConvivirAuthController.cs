using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Dtos;
using Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.ConvivirModule.Features.AuthFeature.Presentation
{
    /// <summary>
    /// Acceso a la app Convivir Abril. Todo es anónimo: es justamente lo que da (o renueva) la
    /// sesión, y la credencial viaja en el cuerpo (contraseña, session token o enlace del correo).
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/convivir/auth")]
    public class ConvivirAuthController : ControllerBase
    {
        private const string ErrorServidor = "Error del servidor. Por favor contactar al administrador del sistema.";

        private readonly IConvivirAuthService _service;

        public ConvivirAuthController(IConvivirAuthService service)
        {
            _service = service;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(ConvivirLoginDto dto)
        {
            try
            {
                return Ok(await _service.Login(dto));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(ConvivirSesionDto dto)
        {
            try
            {
                return Ok(await _service.Refresh(dto.SessionToken));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(ConvivirSesionDto dto)
        {
            try
            {
                await _service.Logout(dto.SessionToken);
                return Ok();
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        [HttpGet("invitacion")]
        public async Task<IActionResult> GetInvitacion([FromQuery] string token)
        {
            try
            {
                return Ok(await _service.GetInvitacion(token));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        [HttpPost("crear-contrasena")]
        public async Task<IActionResult> CrearContrasena(ConvivirCrearContrasenaDto dto)
        {
            try
            {
                return Ok(await _service.CrearContrasena(dto));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        [HttpPost("olvide-contrasena")]
        public async Task<IActionResult> OlvideContrasena(ConvivirOlvideContrasenaDto dto)
        {
            try
            {
                await _service.OlvideContrasena(dto);
                return Ok(new { message = "Si el correo tiene una cuenta de Convivir Abril, te llegará un enlace para crear una nueva contraseña." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        /// <summary>
        /// Destino del botón del correo: una página que abre la app en «crear contraseña». Existe
        /// porque los clientes de correo no vuelven clicable un deep link (abrilconvivir://).
        /// No valida el token: eso lo hace la app al abrirse (GET invitacion).
        /// </summary>
        [HttpGet("abrir")]
        public IActionResult Abrir([FromQuery] string token)
        {
            return Content(_service.PaginaAbrirApp(token), "text/html; charset=utf-8");
        }
    }
}
