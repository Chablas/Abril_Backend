using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.ConfigurationModule.Features.RazonSocialFeature.Presentation
{
    /// <summary>
    /// Configuración → Razones Sociales: las empresas del sistema (propias y de terceros), su
    /// estado y el banco con el que trabaja cada una del grupo.
    /// </summary>
    [ApiController]
    [Route("api/v1/configuracion/razones-sociales")]
    [Authorize]
    [RequireFeature("configuracion.companies")]
    public class RazonSocialController : ControllerBase
    {
        private readonly IRazonSocialService _service;
        private readonly ILogger<RazonSocialController> _logger;

        public RazonSocialController(IRazonSocialService service, ILogger<RazonSocialController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>Carga inicial: tabla completa + catálogo de bancos, en una sola petición.</summary>
        [HttpGet]
        public async Task<IActionResult> GetBandeja()
        {
            try { return Ok(await _service.GetBandeja()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RazonSocialController.GetBandeja");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Consulta de RUC a SUNAT para el alta.</summary>
        [HttpGet("ruc/{ruc}")]
        public async Task<IActionResult> ConsultarRuc(string ruc)
        {
            try
            {
                var result = await _service.ConsultarRuc(ruc);
                if (result is null)
                    return NotFound(new { message = "No se encontró información para el RUC proporcionado." });
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RazonSocialController.ConsultarRuc");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RazonSocialCreateDto dto)
        {
            try { return Ok(await _service.Create(dto, UserId())); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RazonSocialController.Create");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] RazonSocialUpdateDto dto)
        {
            try { return Ok(await _service.Update(id, dto, UserId())); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RazonSocialController.Update");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Id del usuario autenticado (null si el token no lo trae).</summary>
        private int? UserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
    }
}
