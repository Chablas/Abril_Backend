using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Presentation
{
    [ApiController]
    [Route("api/v1/planeamiento-bim/configuracion")]
    [Authorize(Roles = $"{Roles.AdministradorSistema},{Roles.AdministradorUdp},{Roles.UsuarioUdp}")]
    public class PlaneamientoBimConfiguracionController : ControllerBase
    {
        private readonly IPlaneamientoBimConfiguracionService _service;

        public PlaneamientoBimConfiguracionController(IPlaneamientoBimConfiguracionService service)
        {
            _service = service;
        }

        [HttpGet("responsables")]
        public async Task<IActionResult> GetResponsables()
        {
            try
            {
                return Ok(await _service.GetResponsables());
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

        [HttpGet("{projectId:int}")]
        public async Task<IActionResult> GetConfiguracion(int projectId)
        {
            try
            {
                return Ok(await _service.GetConfiguracion(projectId));
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

        [HttpPut("{projectId:int}")]
        public async Task<IActionResult> GuardarConfiguracion(int projectId, [FromBody] ConfiguracionInicialUpdateDto dto)
        {
            try
            {
                await _service.GuardarConfiguracion(projectId, dto);
                return NoContent();
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
