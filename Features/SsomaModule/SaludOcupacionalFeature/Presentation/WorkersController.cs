using System.Text.RegularExpressions;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/workers")]
    [AllowAnonymous]
    public class WorkersController : ControllerBase
    {
        private readonly IWorkerSearchService _service;
        private readonly ILogger<WorkersController> _logger;

        public WorkersController(IWorkerSearchService service, ILogger<WorkersController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int limit = 20)
        {
            try { return Ok(await _service.Search(q, limit)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en WorkersController.Search");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] WorkerCreateDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.ApellidoNombre))
                    return BadRequest(new { message = "apellidoNombre es obligatorio." });
                if (string.IsNullOrWhiteSpace(dto.Dni) || !Regex.IsMatch(dto.Dni, @"^\d{8}$"))
                    return BadRequest(new { message = "dni debe tener exactamente 8 dígitos numéricos." });

                var id = await _service.Create(dto);
                return StatusCode(201, new { id, message = "Trabajador creado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en WorkersController.Create");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] WorkerUpdateDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.ApellidoNombre))
                    return BadRequest(new { message = "apellidoNombre es obligatorio." });

                await _service.Update(id, dto);
                return Ok(new { message = "Trabajador actualizado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en WorkersController.Update");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("{id:int}/retirar")]
        public async Task<IActionResult> Retirar(int id)
        {
            try
            {
                await _service.Retirar(id);
                return Ok(new { message = "Trabajador retirado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en WorkersController.Retirar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
