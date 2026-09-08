using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.ConfigurationModule.Features.BancoFeature.Presentation
{
    /// <summary>
    /// Configuración → Bancos: el catálogo con el que se arma el desplegable «Banco» de las razones
    /// sociales del grupo.
    /// </summary>
    [ApiController]
    [Route("api/v1/configuracion/bancos")]
    [Authorize]
    public class BancoController : ControllerBase
    {
        private readonly IBancoService _service;
        private readonly ILogger<BancoController> _logger;

        public BancoController(IBancoService service, ILogger<BancoController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>
        /// Catálogo completo. Lo consumen la pantalla de Bancos y el modal de razones sociales, así
        /// que el permiso se resuelve con cualquiera de las dos features.
        /// </summary>
        [HttpGet]
        [RequireFeature("configuracion.bancos", "configuracion.companies")]
        public async Task<IActionResult> List()
        {
            try { return Ok(await _service.List()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en BancoController.List");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost]
        [RequireFeature("configuracion.bancos")]
        public async Task<IActionResult> Create([FromBody] BancoUpsertDto dto)
        {
            try { return Ok(await _service.Create(dto, UserId())); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en BancoController.Create");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPut("{id:int}")]
        [RequireFeature("configuracion.bancos")]
        public async Task<IActionResult> Update(int id, [FromBody] BancoUpsertDto dto)
        {
            try { return Ok(await _service.Update(id, dto, UserId())); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en BancoController.Update");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpDelete("{id:int}")]
        [RequireFeature("configuracion.bancos")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.Delete(id, UserId());
                return Ok(new { message = "Banco eliminado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en BancoController.Delete");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Id del usuario autenticado (null si el token no lo trae).</summary>
        private int? UserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
    }
}
