using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.AreasYSubareasFeature.Application.Dtos;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.AreasYSubareasFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.AreasYSubareasFeature.Presentation
{
    [ApiController]
    [Route("api/v1/psss-scope")]
    public class PsssScopeController : ControllerBase
    {
        private readonly IPsssScopeService _service;

        public PsssScopeController(IPsssScopeService service)
        {
            _service = service;
        }

        [Authorize]
        [HttpGet("area/{areaId}")]
        public async Task<IActionResult> GetByArea(int areaId)
        {
            try
            {
                return Ok(await _service.GetByAreaAsync(areaId));
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("subarea/{subAreaId}")]
        public async Task<IActionResult> GetBySubArea(int subAreaId)
        {
            try
            {
                return Ok(await _service.GetBySubAreaAsync(subAreaId));
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPut("area/{areaId}")]
        public async Task<IActionResult> UpdateByArea(int areaId, [FromBody] UpdateScopePsssDTO dto)
        {
            try
            {
                await _service.UpdateByAreaAsync(areaId, dto.PsssIds);
                return Ok(new { message = "Relaciones de área actualizadas." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPut("subarea/{subAreaId}")]
        public async Task<IActionResult> UpdateBySubArea(int subAreaId, [FromBody] UpdateScopePsssDTO dto)
        {
            try
            {
                await _service.UpdateBySubAreaAsync(subAreaId, dto.PsssIds);
                return Ok(new { message = "Relaciones de subárea actualizadas." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
