using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Interconsulta;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional/interconsultas")]
    [Authorize]
    public class InterconsultaController : ControllerBase
    {
        private readonly IInterconsultaService _service;
        private readonly ILogger<InterconsultaController> _logger;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ISharePointHabService _sharePoint;

        public InterconsultaController(
            IInterconsultaService service,
            ILogger<InterconsultaController> logger,
            IDbContextFactory<AppDbContext> factory,
            ISharePointHabService sharePoint)
        {
            _service = service;
            _logger = logger;
            _factory = factory;
            _sharePoint = sharePoint;
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

        [HttpPost("{id:int}/documentos")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> SubirDocumento(int id, [FromForm] IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new AbrilException("El archivo es obligatorio.", 400);

                using var ctx = _factory.CreateDbContext();
                var interconsulta = await ctx.SsInterconsulta.FirstOrDefaultAsync(i => i.Id == id)
                    ?? throw new AbrilException("Interconsulta no encontrada.", 404);

                string url;
                using (var stream = file.OpenReadStream())
                    url = await _sharePoint.SubirArchivoAsync(stream, file.FileName, "interconsulta");

                interconsulta.UrlInforme = url;
                interconsulta.UpdatedAt = DateTimeOffset.UtcNow;
                await ctx.SaveChangesAsync();

                return Ok(new { url });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en InterconsultaController.SubirDocumento"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
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
