using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Presentation
{
    /// <summary>
    /// Sección "Pasos" de Configuración de Costos: opciones que se prenden/apagan por paso
    /// del flujo de adjudicaciones.
    /// </summary>
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    [RequireFeature("costs.config.pasos")]
    public class CostsPasosController : ControllerBase
    {
        private readonly ICostsPasoService _service;

        public CostsPasosController(ICostsPasoService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                return Ok(await _service.GetPasosAsync());
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPut("option")]
        public async Task<IActionResult> UpdateOption([FromBody] CostsPasoOptionUpdateDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                await _service.UpdateOptionAsync(dto, userId);
                return Ok(new { message = "Configuración guardada." });
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
