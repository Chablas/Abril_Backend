using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Interconsulta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/interconsultas")]
    [Authorize]
    public class InterconsultaController : ControllerBase
    {
        private readonly IInterconsultaService _service;
        private readonly ILogger<InterconsultaController> _logger;

        public InterconsultaController(IInterconsultaService service, ILogger<InterconsultaController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int? CurrentUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] InterconsultaFilterDto filter)
        {
            try { return Ok(await _service.List(filter)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en InterconsultaController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] InterconsultaCreateDto dto)
        {
            try
            {
                var id = await _service.Create(dto, CurrentUserId());
                return Ok(new { id, message = "Interconsulta registrada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en InterconsultaController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] InterconsultaUpdateDto dto)
        {
            try
            {
                await _service.Update(id, dto, CurrentUserId());
                return Ok(new { message = "Interconsulta actualizada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en InterconsultaController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("{id:int}/resultado")]
        public async Task<IActionResult> PatchResultado(int id, [FromBody] InterconsultaResultadoPatchDto dto)
        {
            try
            {
                await _service.UpdateResultado(id, dto, CurrentUserId());
                return Ok(new { message = "Resultado de interconsulta actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en InterconsultaController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
