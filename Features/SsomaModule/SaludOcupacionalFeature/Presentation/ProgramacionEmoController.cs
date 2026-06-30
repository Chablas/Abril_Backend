using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/programaciones")]
    [Authorize]
    public class ProgramacionEmoController : ControllerBase
    {
        private readonly IProgramacionEmoService _service;
        private readonly ILogger<ProgramacionEmoController> _logger;

        public ProgramacionEmoController(IProgramacionEmoService service, ILogger<ProgramacionEmoController> logger)
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
        public async Task<IActionResult> GetList([FromQuery] ProgramacionFilterDto filter)
        {
            // Usuarios internos (no clínica) ven todas las programaciones aunque haya interconsulta pendiente
            filter.IncluirConInterconsulta = !User.IsInRole("CLINICA");
            try { return Ok(await _service.List(filter)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ProgramacionEmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ProgramacionCreateDto dto)
        {
            try
            {
                var id = await _service.Create(dto, CurrentUserId());
                return Ok(new { id, message = "Programación registrada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ProgramacionEmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProgramacionUpdateDto dto)
        {
            try
            {
                await _service.Update(id, dto, CurrentUserId());
                return Ok(new { message = "Programación actualizada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ProgramacionEmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("{id:int}/estado")]
        public async Task<IActionResult> PatchEstado(int id, [FromBody] ProgramacionEstadoPatchDto dto)
        {
            try
            {
                await _service.UpdateEstado(id, dto.Estado, dto.EmoResultadoId, CurrentUserId());
                return Ok(new { message = "Estado de programación actualizado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ProgramacionEmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("{id:int}/clinica-accion")]
        public async Task<IActionResult> ClinicaAccion(int id, [FromBody] ProgramacionClinicaAccionDto dto)
        {
            try
            {
                await _service.ClinicaAccion(id, dto, CurrentUserId());
                return Ok(new { message = "Acción registrada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ProgramacionEmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("habilitacion")]
        public async Task<IActionResult> GetHabilitacion(
            [FromQuery] string? estado,
            [FromQuery] int? proyectoId,
            [FromQuery] string? fecha,
            [FromQuery] bool? soloNoNotificados)
        {
            try
            {
                var filtros = new ProgramacionHabilitacionFiltrosDto
                {
                    Estado             = estado,
                    ProyectoId         = proyectoId,
                    Fecha              = fecha,
                    SoloNoNotificados  = soloNoNotificados,
                };
                var result = await _service.GetHabilitacionAsync(filtros);
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error GetHabilitacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("{id:int}/notificado")]
        public async Task<IActionResult> PatchNotificado(int id, [FromBody] PatchNotificadoDto dto)
        {
            try
            {
                await _service.PatchNotificadoAsync(id, dto.Notificado);
                return NoContent();
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error PatchNotificado"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("{id:int}/deshacer-checkin")]
        public async Task<IActionResult> DeshacerCheckIn(int id)
        {
            try
            {
                await _service.UndoCheckInAsync(id);
                return Ok(new { message = "Ingreso deshecho. Estado revertido a 'Aceptado por Clínica'." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en ProgramacionEmoController.DeshacerCheckIn"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
