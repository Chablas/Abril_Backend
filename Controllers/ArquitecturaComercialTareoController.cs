using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Application.DTOs.ArquitecturaComercial;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Application.Interfaces;

namespace Abril_Backend.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/arquitectura-comercial/tareo")]
    public class ArquitecturaComercialTareoController : ControllerBase
    {
        private readonly IArquitecturaComercialTareoService _service;
        private readonly ILogger<ArquitecturaComercialTareoController> _logger;

        public ArquitecturaComercialTareoController(
            IArquitecturaComercialTareoService service,
            ILogger<ArquitecturaComercialTareoController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // Mismo criterio que ArquitecturaComercialController: el claim NameIdentifier se usa
        // directamente como el id de Worker en este dominio (ver GetActividades).
        private int? CurrentWorkerId()
            => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;

        [HttpGet("enrolamiento/estado")]
        public async Task<IActionResult> GetEnrolamientoEstado()
        {
            var workerId = CurrentWorkerId();
            if (workerId == null) return Unauthorized();

            try
            {
                return Ok(await _service.GetEnrolamientoEstado(workerId.Value));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando estado de enrolamiento de tareo");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("enrolamiento")]
        public async Task<IActionResult> Enrolar([FromBody] TareoEnrolamientoRequestDTO body)
        {
            var workerId = CurrentWorkerId();
            if (workerId == null) return Unauthorized();

            try
            {
                await _service.EnrolarWorker(workerId.Value, body);
                return Ok(new { message = "Enrolamiento registrado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en enrolamiento de tareo");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("marcar")]
        public async Task<IActionResult> Marcar([FromBody] TareoMarcarRequestDTO body, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKeyHeader)
        {
            var workerId = CurrentWorkerId();
            if (workerId == null) return Unauthorized();

            if (!Guid.TryParse(idempotencyKeyHeader, out var idempotencyKey))
                return BadRequest(new { message = "Falta el header Idempotency-Key (debe ser un GUID generado por el cliente)." });

            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var result = await _service.Marcar(workerId.Value, idempotencyKey, body, ip);
                return result.YaExistia ? Ok(result) : StatusCode(201, result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marcando tareo");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("mi-tareo/hoy")]
        public async Task<IActionResult> GetMiTareoHoy()
        {
            var workerId = CurrentWorkerId();
            if (workerId == null) return Unauthorized();

            try
            {
                return Ok(await _service.GetMiTareoHoy(workerId.Value));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando tareo del día");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("registros")]
        public async Task<IActionResult> GetRegistros([FromQuery] TareoFiltroDTO filtro)
        {
            try
            {
                return Ok(await _service.GetRegistros(filtro));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listando registros de tareo");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("registros/{id}/revisar")]
        public async Task<IActionResult> Revisar(int id, [FromBody] TareoRevisarRequestDTO body)
        {
            var revisorId = CurrentWorkerId();
            if (revisorId == null) return Unauthorized();

            try
            {
                var ok = await _service.Revisar(id, revisorId.Value, body);
                if (!ok) return NotFound(new { message = "Registro de tareo no encontrado." });
                return Ok(new { message = "Registro actualizado." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revisando registro de tareo");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("reporte-semanal")]
        public async Task<IActionResult> GetReporteSemanal([FromQuery] int? proyectoId, [FromQuery] DateOnly semana)
        {
            try
            {
                return Ok(await _service.GetReporteSemanal(proyectoId, semana));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando reporte semanal de tareo");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
