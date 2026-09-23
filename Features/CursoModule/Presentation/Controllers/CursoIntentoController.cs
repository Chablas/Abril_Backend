using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.CursoModule.Application.Dtos;
using Abril_Backend.Features.CursoModule.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.CursoModule.Presentation.Controllers
{
    [ApiController]
    [Route("api/v1/curso-intento")]
    [Authorize]
    public class CursoIntentoController : ControllerBase
    {
        private readonly ICursoIntentoService _service;
        private readonly ILogger<CursoIntentoController> _logger;

        public CursoIntentoController(ICursoIntentoService service, ILogger<CursoIntentoController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int GetUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        [HttpPost("iniciar")]
        public async Task<IActionResult> Iniciar([FromBody] IniciarIntentoDto dto)
        {
            try
            {
                // La IP y el User-Agent SIEMPRE se capturan en backend (nunca del cliente),
                // para que la evidencia de auditoría sea confiable.
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                var userAgent = Request.Headers.UserAgent.ToString();

                var result = await _service.IniciarAsync(GetUserId(), dto, ipAddress, userAgent);
                return StatusCode(201, result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoIntentoController.Iniciar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{intentoId:int}/responder")]
        public async Task<IActionResult> Responder(int intentoId, [FromBody] ResponderSlideDto dto)
        {
            try
            {
                var result = await _service.ResponderAsync(intentoId, dto);
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoIntentoController.Responder"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{intentoId:int}/finalizar")]
        public async Task<IActionResult> Finalizar(int intentoId, [FromBody] FinalizarIntentoDto dto)
        {
            try
            {
                var result = await _service.FinalizarAsync(intentoId, dto);
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoIntentoController.Finalizar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("{intentoId:int}")]
        public async Task<IActionResult> GetDetalle(int intentoId)
        {
            try
            {
                var detalle = await _service.GetDetalleAsync(intentoId);
                return Ok(detalle);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CursoIntentoController.GetDetalle"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
