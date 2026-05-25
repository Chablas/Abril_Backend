using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.ContratistaUsuarios;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Habilitacion.Presentation
{
    [ApiController]
    [Route("api/v1/contratista-usuarios")]
    [Authorize]
    public class ContratistaUsuarioController : ControllerBase
    {
        private readonly IContratistaUsuarioService _service;
        private readonly ILogger<ContratistaUsuarioController> _logger;

        public ContratistaUsuarioController(
            IContratistaUsuarioService service,
            ILogger<ContratistaUsuarioController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsuarios([FromQuery] int contractorId)
        {
            try
            {
                var result = await _service.GetUsuariosAsync(contractorId);
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ContratistaUsuarioController.GetUsuarios"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        public async Task<IActionResult> InvitarUsuario(
            [FromQuery] int contractorId,
            [FromBody] ContratistaUsuarioCreateDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _service.InvitarUsuarioAsync(contractorId, dto, userId);
                return StatusCode(201, new { message = "Usuario invitado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ContratistaUsuarioController.InvitarUsuario"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> ActualizarUsuario(
            int id,
            [FromQuery] int contractorId,
            [FromBody] ContratistaUsuarioUpdateDto dto)
        {
            try
            {
                await _service.ActualizarUsuarioAsync(id, contractorId, dto);
                return Ok(new { message = "Usuario actualizado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ContratistaUsuarioController.ActualizarUsuario"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DesactivarUsuario(int id, [FromQuery] int contractorId)
        {
            try
            {
                await _service.DesactivarUsuarioAsync(id, contractorId);
                return Ok(new { message = "Usuario desactivado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ContratistaUsuarioController.DesactivarUsuario"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
