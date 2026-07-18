using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.DescansoMedico;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional")]
    [Authorize]
    [RequireFeature("ssoma.salud-ocupacional.descansos")]
    public class DescansoMedicoController : ControllerBase
    {
        private readonly IDescansoMedicoService _service;
        private readonly IFileStorageService _storage;
        private readonly ILogger<DescansoMedicoController> _logger;

        public DescansoMedicoController(IDescansoMedicoService service, IFileStorageService storage, ILogger<DescansoMedicoController> logger)
        {
            _service = service;
            _storage = storage;
            _logger = logger;
        }

        private int? CurrentUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(val, out var id) ? id : (int?)null;
        }

        [HttpGet("descansos")]
        public async Task<IActionResult> GetList([FromQuery] DescansoMedicoFilterDto filter)
        {
            try { return Ok(await _service.ListPaged(filter)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en DescansoMedicoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("descansos/{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try { return Ok(await _service.GetById(id)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en DescansoMedicoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("descansos")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] DescansoMedicoCreateDto dto, IFormFile? archivoCertificado)
        {
            try
            {
                if (archivoCertificado is { Length: > 0 })
                {
                    var urls = await _storage.UploadFilesAsync(
                        [(archivoCertificado.OpenReadStream(), archivoCertificado.FileName)],
                        "ssoma-descansos");
                    dto.UrlCertificado = urls.FirstOrDefault();
                }
                var id = await _service.Create(dto, CurrentUserId());
                return Ok(new { id, message = "Descanso médico registrado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en DescansoMedicoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("descansos/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] DescansoMedicoUpdateDto dto)
        {
            try
            {
                await _service.Update(id, dto);
                return Ok(new { message = "Descanso médico actualizado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en DescansoMedicoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("descansos/{id:int}/aprobar")]
        public async Task<IActionResult> Aprobar(int id, [FromBody] DescansoAprobarDto dto)
        {
            try
            {
                await _service.Aprobar(id, dto, CurrentUserId());
                return Ok(new { message = "Descanso médico aprobado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en DescansoMedicoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("descansos/{id:int}/rechazar")]
        public async Task<IActionResult> Rechazar(int id, [FromBody] DescansoRechazarDto dto)
        {
            try
            {
                await _service.Rechazar(id, dto, CurrentUserId());
                return Ok(new { message = "Descanso médico rechazado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en DescansoMedicoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("descansos/{id:int}/alta")]
        public async Task<IActionResult> DarAlta(int id, [FromBody] DarAltaDto dto)
        {
            try
            {
                await _service.DarAlta(id, dto, CurrentUserId());
                return Ok(new { message = "Alta médica registrada. El trabajador ha sido desbloqueado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en DescansoMedicoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("descansos/{id:int}/seguimientos")]
        public async Task<IActionResult> GetSeguimientos(int id)
        {
            try { return Ok(await _service.GetSeguimientos(id)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en DescansoMedicoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("descansos/{id:int}/seguimientos")]
        public async Task<IActionResult> CreateSeguimiento(int id, [FromBody] DescansoSeguimientoCreateDto dto)
        {
            try
            {
                // Etiqueta de "quién registró (por rol)" para mostrar; usa el nombre, no el ID.
                var rolClaim = User.FindFirst("role_name")?.Value;
                var seguimientoId = await _service.CreateSeguimiento(id, dto, CurrentUserId(), rolClaim);
                return Ok(new { id = seguimientoId, message = "Seguimiento registrado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en DescansoMedicoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("descansos/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.Delete(id);
                return Ok(new { message = "Descanso médico eliminado exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en DescansoMedicoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
