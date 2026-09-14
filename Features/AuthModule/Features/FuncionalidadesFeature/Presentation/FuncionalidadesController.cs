using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.AuthModule.Features.FuncionalidadesFeature.Presentation
{
    /// <summary>
    /// Seguridad → Funcionalidades. Solo lectura: no hay alta, edición ni baja desde la interfaz.
    /// </summary>
    [ApiController]
    [Route("api/v1/funcionalidades")]
    [Authorize]
    [RequireFeature("security.features")]
    public class FuncionalidadesController : ControllerBase
    {
        private readonly IFuncionalidadesService _service;
        private readonly ILogger<FuncionalidadesController> _logger;

        public FuncionalidadesController(IFuncionalidadesService service, ILogger<FuncionalidadesController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            try { return Ok(await _service.List()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en FuncionalidadesController.List");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("{featureId:int}")]
        public async Task<IActionResult> GetDetalle(int featureId)
        {
            try { return Ok(await _service.GetDetalle(featureId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en FuncionalidadesController.GetDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
