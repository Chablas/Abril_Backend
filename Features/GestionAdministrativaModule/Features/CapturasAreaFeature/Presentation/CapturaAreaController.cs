using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.CapturasArea.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.CapturasArea.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionAdministrativa.CapturasArea.Presentation
{
    /// <summary>
    /// Configuración → Capturas: qué áreas exigen capturas de movilidad para rendir una salida.
    /// El acceso a la sección se controla igual que el resto de la configuración de salidas, con el
    /// featureKey <c>gestion-administrativa.config.capturas</c> (tablas feature/role_feature).
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/configuracion/capturas")]
    [Authorize]
    public class CapturaAreaController : ControllerBase
    {
        private readonly ICapturaAreaService _service;
        private readonly ILogger<CapturaAreaController> _logger;

        public CapturaAreaController(ICapturaAreaService service, ILogger<CapturaAreaController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>Carga inicial: todas las áreas activas con su flag + opciones de los filtros.</summary>
        [HttpGet]
        public async Task<IActionResult> GetInitialData()
        {
            try   { return Ok(await _service.GetInitialDataAsync()); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CapturaAreaController.GetInitialData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Marca las capturas del área como obligatorias u opcionales.</summary>
        [HttpPut("{areaScopeId:int}")]
        public async Task<IActionResult> SetCapturasObligatorias(int areaScopeId, [FromBody] CapturaAreaUpdateDto dto)
        {
            try
            {
                await _service.SetCapturasObligatoriasAsync(areaScopeId, dto.CapturasObligatorias);
                return Ok(new
                {
                    message = dto.CapturasObligatorias
                        ? "Las capturas ahora son obligatorias para el área."
                        : "Las capturas ahora son opcionales para el área."
                });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CapturaAreaController.SetCapturasObligatorias");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
