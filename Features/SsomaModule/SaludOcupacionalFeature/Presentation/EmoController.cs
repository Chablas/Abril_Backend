using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional")]
    [Authorize]
    public class EmoController : ControllerBase
    {
        private readonly IEmoService _service;
        private readonly ILogger<EmoController> _logger;

        public EmoController(IEmoService service, ILogger<EmoController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int? CurrentUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        [HttpGet("emos")]
        public async Task<IActionResult> GetList([FromQuery] EmoFilterDto filter)
        {
            try { return Ok(await _service.ListPaged(filter)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("emos/{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try { return Ok(await _service.GetById(id)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("workers/{workerId:int}/historial-emo")]
        public async Task<IActionResult> GetHistorial(int workerId)
        {
            try { return Ok(await _service.GetHistorialByWorker(workerId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("emos")]
        public async Task<IActionResult> Create([FromBody] EmoCreateDto dto)
        {
            try
            {
                var id = await _service.Create(dto, CurrentUserId());
                return Ok(new { id, message = "EMO registrado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("emos/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] EmoUpdateDto dto)
        {
            try
            {
                await _service.Update(id, dto, CurrentUserId());
                return Ok(new { message = "EMO actualizado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("emos/{id:int}/estado")]
        public async Task<IActionResult> PatchEstado(int id, [FromBody] EmoEstadoPatchDto dto)
        {
            try
            {
                await _service.UpdateEstado(id, dto.Estado, CurrentUserId());
                return Ok(new { message = "Estado del EMO actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
