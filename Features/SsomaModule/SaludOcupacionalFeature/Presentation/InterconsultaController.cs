using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Interconsulta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Abril_Backend.Shared.Constants;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/interconsultas")]
    [Authorize]
    public class InterconsultaController : ControllerBase
    {
        private readonly IInterconsultaService _service;
        private readonly ILogger<InterconsultaController> _logger;
        private readonly ISharePointHabService _sharePoint;
        private readonly IDbContextFactory<AppDbContext> _factory;

        public InterconsultaController(
            IInterconsultaService service,
            ILogger<InterconsultaController> logger,
            ISharePointHabService sharePoint,
            IDbContextFactory<AppDbContext> factory)
        {
            _service = service;
            _logger = logger;
            _sharePoint = sharePoint;
            _factory = factory;
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

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try { return Ok(await _service.GetById(id)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en InterconsultaController.GetById"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        [Authorize(Roles = Roles.Clinica)]
        public async Task<IActionResult> Create([FromBody] InterconsultaCreateDto dto)
        {
            try
            {
                var id = await _service.Create(dto, CurrentUserId());
                return Ok(new { id, message = "Interconsulta registrada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error al crear interconsulta"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = Roles.Clinica)]
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

        /// <summary>
        /// Sube el levantamiento (informe) de la interconsulta.
        /// Solo la clínica puede hacer esto. Al subir el documento,
        /// la interconsulta pasa a "Atendida" y la programación a "En Atención".
        /// </summary>
        [HttpPost("{id}/documentos")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = Roles.Clinica)]
        public async Task<IActionResult> SubirDocumento(int id, [FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "Archivo requerido." });

                using var ctx = _factory.CreateDbContext();
                var interconsulta = await ctx.SsInterconsulta.FindAsync(id)
                    ?? throw new AbrilException("Interconsulta no encontrada.", 404);

                using var stream = file.OpenReadStream();
                var path = await _sharePoint.SubirArchivoAsync(stream, file.FileName, "interconsulta");
                interconsulta.UrlInforme = path;
                interconsulta.Estado = "Atendida";
                interconsulta.UpdatedAt = DateTimeOffset.UtcNow;

                // Retornar la programación a "En Atención" para que la clínica pueda completar el proceso.
                // No exigimos Estado == "En Interconsulta" exacto: si la programación ya quedó
                // "Completado" por otra vía (p. ej. el EMO se registró sin marcar que requería
                // interconsulta, y esta se creó aparte), igual debe poder avanzar al resolverse.
                var prog = await ctx.SsProgramacionEmo
                    .Where(p => p.WorkerId == interconsulta.WorkerId
                             && p.Estado != "Completado"
                             && p.Estado != "Cancelado"
                             && p.Estado != "Rechazado por Clínica")
                    .OrderByDescending(p => p.FechaProgramada)
                    .FirstOrDefaultAsync();
                if (prog != null)
                {
                    prog.Estado = "En Atención";
                    prog.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await ctx.SaveChangesAsync();

                return Ok(new { url = path });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error subiendo documento interconsulta {Id}", id); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("{id:int}/resultado")]
        [Authorize(Roles = Roles.Clinica)]
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

        [HttpPatch("{id:int}/derivacion")]
        [Authorize(Roles = Roles.Clinica)]
        public async Task<IActionResult> PatchDerivacion(int id, [FromBody] InterconsultaDerivacionPatchDto dto)
        {
            try
            {
                await _service.UpdateDerivacion(id, dto, CurrentUserId());
                return Ok(new { message = "Derivación actualizada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en InterconsultaController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
