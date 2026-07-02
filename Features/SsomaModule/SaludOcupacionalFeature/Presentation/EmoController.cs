using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Interfaces;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo;
using Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces;
using Abril_Backend.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Presentation
{
    [ApiController]
    [Route("api/v1/ssoma/salud-ocupacional")]
    [Authorize]
    public class EmoController : ControllerBase
    {
        private readonly IEmoService _service;
        private readonly ILogger<EmoController> _logger;
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ISharePointHabService _sharePoint;

        public EmoController(
            IEmoService service,
            ILogger<EmoController> logger,
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

        [HttpGet("emos")]
        public async Task<IActionResult> GetList([FromQuery] EmoFilterDto filter)
        {
            try { return Ok(await _service.ListPaged(filter)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoController"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("emos/por-trabajador")]
        public async Task<IActionResult> GetPorTrabajador([FromQuery] EmoPorTrabajadorFilterDto filter)
        {
            try { return Ok(await _service.ListPorTrabajador(filter)); }
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
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create(
            [FromForm] EmoCreateDto dto,
            [FromForm] IFormFile? documentoInterconsulta,
            [FromForm] IFormFile? archivoLectura)
        {
            try
            {
                dto.DocumentoInterconsulta = documentoInterconsulta;
                dto.ArchivoLectura = archivoLectura;
                var result = await _service.Create(dto, CurrentUserId());
                return Ok(new { id = result.EmoId, interconsultaId = result.InterconsultaId, message = "EMO registrado exitosamente." });
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

        [HttpPost("emos/{emoId:int}/documentos")]
        public async Task<IActionResult> SubirDocumento(int emoId, [FromForm] IFormFile file, [FromForm] string tipo)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new AbrilException("El archivo es obligatorio.", 400);

                var tipoNorm = tipo?.Trim() ?? string.Empty;
                if (tipoNorm != "Aptitud" && tipoNorm != "EMO" && tipoNorm != "Lectura")
                    throw new AbrilException("El tipo debe ser 'Aptitud', 'EMO' o 'Lectura'.", 400);

                using var ctx = _factory.CreateDbContext();

                var emo = await ctx.WorkerEmo
                    .Include(e => e.Worker).ThenInclude(w => w!.Person)
                    .FirstOrDefaultAsync(e => e.Id == emoId)
                    ?? throw new AbrilException("EMO no encontrado.", 404);

                // Si el solicitante es una clínica, validar que el EMO le pertenezca
                if (User.IsInRole("CLINICA"))
                {
                    var clinicaIdClaim = User.FindFirst("clinicaId")?.Value;
                    if (!int.TryParse(clinicaIdClaim, out var clinicaId) || emo.ClinicaId != clinicaId)
                        throw new AbrilException("No tiene permiso para subir documentos de este EMO.", 403);
                    // La clínica solo puede subir Aptitud y EMO completo, no Lectura
                    if (tipoNorm == "Lectura")
                        throw new AbrilException("La clínica no puede subir archivos de lectura de EMO.", 403);
                }

                var dni = emo.Worker?.Person?.DocumentIdentityCode ?? emo.WorkerId.ToString();
                var fecha = DateTime.UtcNow.ToString("yyyyMMdd");
                var contexto = tipoNorm == "Aptitud" ? "emo-aptitud"
                             : tipoNorm == "Lectura" ? "lectura-emo"
                             : "emo-completo";
                var fileName = $"{dni}_{tipoNorm}_{fecha}.pdf";

                string path;
                using (var stream = file.OpenReadStream())
                    path = await _sharePoint.SubirArchivoAsync(stream, fileName, contexto);

                if (tipoNorm == "Aptitud")
                    emo.UrlAptitud = path;
                else if (tipoNorm == "Lectura")
                    emo.UrlResultado = path;
                else
                    emo.UrlEmoCompleto = path;

                await ctx.SaveChangesAsync();

                return Ok(new { url = path });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EmoController.SubirDocumento"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
