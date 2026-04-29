using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Catalogos;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/catalogos")]
    [AllowAnonymous]
    public class CatalogosController : ControllerBase
    {
        private readonly ICatalogosService _service;
        private readonly ILogger<CatalogosController> _logger;

        public CatalogosController(ICatalogosService service, ILogger<CatalogosController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // ===== Clinicas =====
        [HttpGet("clinicas")]
        public async Task<IActionResult> GetClinicas([FromQuery] bool soloActivos = true)
        {
            try { return Ok(await _service.ListClinicas(soloActivos)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("clinicas")]
        public async Task<IActionResult> CreateClinica([FromBody] ClinicaUpsertDto dto)
        {
            try { return Ok(await _service.CreateClinica(dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("clinicas/{id:int}")]
        public async Task<IActionResult> UpdateClinica(int id, [FromBody] ClinicaUpsertDto dto)
        {
            try { return Ok(await _service.UpdateClinica(id, dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ===== Medicos =====
        [HttpGet("medicos")]
        public async Task<IActionResult> GetMedicos([FromQuery] bool soloActivos = true)
        {
            try { return Ok(await _service.ListMedicos(soloActivos)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("medicos")]
        public async Task<IActionResult> CreateMedico([FromBody] MedicoOcupacionalUpsertDto dto)
        {
            try { return Ok(await _service.CreateMedico(dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("medicos/{id:int}")]
        public async Task<IActionResult> UpdateMedico(int id, [FromBody] MedicoOcupacionalUpsertDto dto)
        {
            try { return Ok(await _service.UpdateMedico(id, dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ===== EMO Tipos =====
        [HttpGet("emo-tipos")]
        public async Task<IActionResult> GetEmoTipos([FromQuery] bool soloActivos = true)
        {
            try { return Ok(await _service.ListEmoTipos(soloActivos)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("emo-tipos")]
        public async Task<IActionResult> CreateEmoTipo([FromBody] EmoTipoUpsertDto dto)
        {
            try { return Ok(await _service.CreateEmoTipo(dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("emo-tipos/{id:int}")]
        public async Task<IActionResult> UpdateEmoTipo(int id, [FromBody] EmoTipoUpsertDto dto)
        {
            try { return Ok(await _service.UpdateEmoTipo(id, dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ===== Examen Tipos =====
        [HttpGet("examen-tipos")]
        public async Task<IActionResult> GetExamenTipos([FromQuery] bool soloActivos = true)
        {
            try { return Ok(await _service.ListExamenTipos(soloActivos)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("examen-tipos")]
        public async Task<IActionResult> CreateExamenTipo([FromBody] ExamenTipoUpsertDto dto)
        {
            try { return Ok(await _service.CreateExamenTipo(dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("examen-tipos/{id:int}")]
        public async Task<IActionResult> UpdateExamenTipo(int id, [FromBody] ExamenTipoUpsertDto dto)
        {
            try { return Ok(await _service.UpdateExamenTipo(id, dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ===== Restriccion Tipos =====
        [HttpGet("restriccion-tipos")]
        public async Task<IActionResult> GetRestriccionTipos([FromQuery] bool soloActivos = true)
        {
            try { return Ok(await _service.ListRestriccionTipos(soloActivos)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("restriccion-tipos")]
        public async Task<IActionResult> CreateRestriccionTipo([FromBody] RestriccionTipoUpsertDto dto)
        {
            try { return Ok(await _service.CreateRestriccionTipo(dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("restriccion-tipos/{id:int}")]
        public async Task<IActionResult> UpdateRestriccionTipo(int id, [FromBody] RestriccionTipoUpsertDto dto)
        {
            try { return Ok(await _service.UpdateRestriccionTipo(id, dto)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ===== Empresas =====
        [HttpGet("empresas")]
        public async Task<IActionResult> GetEmpresas([FromQuery] bool soloActivas = true)
        {
            try { return Ok(await _service.ListEmpresas(soloActivas)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en CatalogosController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
