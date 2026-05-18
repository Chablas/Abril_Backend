using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Auth;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Habilitacion.Presentation
{
    [ApiController]
    [Route("api/v1/habilitacion/auth")]
    [AllowAnonymous]
    public class ContratistaAuthController : ControllerBase
    {
        private readonly IContratistaAuthService _service;
        private readonly ILogger<ContratistaAuthController> _logger;

        public ContratistaAuthController(IContratistaAuthService service, ILogger<ContratistaAuthController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ContratistaLoginDto dto)
        {
            try { return Ok(await _service.LoginAsync(dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ContratistaAuthController.Login"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("empresas")]
        public async Task<IActionResult> GetEmpresas()
        {
            try { return Ok(await _service.GetEmpresasParaLoginAsync()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ContratistaAuthController.GetEmpresas"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("activar")]
        public async Task<IActionResult> Activar([FromBody] ActivarCuentaDto dto)
        {
            try { return Ok(await _service.ActivarCuentaAsync(dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ContratistaAuthController.Activar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("solicitar-reset")]
        public async Task<IActionResult> SolicitarReset([FromBody] SolicitarResetDto dto)
        {
            try
            {
                await _service.SolicitarResetPasswordAsync(dto);
                return Ok(new { message = "Si el correo está registrado, recibirás un enlace para restablecer tu contraseña." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ContratistaAuthController.SolicitarReset"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            try
            {
                await _service.ResetPasswordAsync(dto);
                return Ok(new { message = "Contraseña restablecida correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ContratistaAuthController.ResetPassword"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("cambiar-password")]
        [Authorize(Roles = "CONTRATISTA")]
        public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordDto dto)
        {
            try
            {
                var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!int.TryParse(claim, out var userId))
                    return StatusCode(401, new { message = "Token inválido." });

                await _service.CambiarPasswordAsync(userId, dto);
                return Ok(new { message = "Contraseña actualizada correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ContratistaAuthController.CambiarPassword"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
