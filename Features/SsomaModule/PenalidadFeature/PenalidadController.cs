using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.Penalidad.Dtos;
using Abril_Backend.Features.Ssoma.Penalidad.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.Ssoma.Penalidad;

[ApiController]
[Route("api/v1/ssoma-penalidad")]
[Authorize]
public class PenalidadController : ControllerBase
{
    private readonly IPenalidadService _service;
    private readonly ILogger<PenalidadController> _logger;
    private readonly IConfiguration _configuration;

    public PenalidadController(IPenalidadService service, ILogger<PenalidadController> logger, IConfiguration configuration)
    {
        _service       = service;
        _logger        = logger;
        _configuration = configuration;
    }

    private int GetUserId() =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    private bool EsContratista() => User.FindFirst("tipo")?.Value == "CONTRATISTA";

    /// <summary>Penalidad es de una sola vía (Abril → Contratista): un contratista solo
    /// puede ver/operar penalidades de su propia empresa.</summary>
    private int? GetEmpresaIdContratista() =>
        EsContratista() && int.TryParse(User.FindFirst("empresaId")?.Value, out var id) ? id : null;

    private bool EsPropioDeContratista(int empresaId)
    {
        var propia = GetEmpresaIdContratista();
        return !propia.HasValue || empresaId == propia.Value;
    }

    [HttpGet("infracciones")]
    public async Task<IActionResult> GetInfracciones()
    {
        try { return Ok(await _service.GetInfraccionesAsync(soloActivas: true)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.GetInfracciones"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet]
    [RequireFeature("ssoma.gestion.penalidades.lista")]
    public async Task<IActionResult> GetList([FromQuery] PenalidadListQuery q)
    {
        try
        {
            q.EmpresaIdContratista = GetEmpresaIdContratista();
            return Ok(await _service.GetListAsync(q));
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.GetList"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}")]
    [RequireFeature("ssoma.gestion.penalidades.lista")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        try
        {
            var r = await _service.GetDetalleAsync(id);
            if (r is null) return NotFound(new { message = "Penalidad no encontrada." });
            if (!EsPropioDeContratista(r.EmpresaId)) return Forbid();
            return Ok(r);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.GetDetalle"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("origenes-candidatos")]
    [RequireFeature("ssoma.gestion.penalidades.crear")]
    public async Task<IActionResult> GetOrigenesCandidatos([FromQuery] int? empresaId, [FromQuery] int? proyectoId)
    {
        try { return Ok(await _service.GetOrigenesCandidatosAsync(empresaId, proyectoId)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.GetOrigenesCandidatos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost]
    [RequireFeature("ssoma.gestion.penalidades.crear")]
    public async Task<IActionResult> Registrar([FromBody] PenalidadRegistrarRequest req)
    {
        try { return StatusCode(201, await _service.RegistrarAsync(req, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.Registrar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{id:int}/aprobar-residente")]
    [RequireFeature("ssoma.gestion.penalidades.aprobar-residente")]
    public async Task<IActionResult> AprobarResidente(int id)
    {
        try { return Ok(await _service.AprobarResidenteAsync(id, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.AprobarResidente"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{id:int}/rechazar-residente")]
    [RequireFeature("ssoma.gestion.penalidades.aprobar-residente")]
    public async Task<IActionResult> RechazarResidente(int id, [FromBody] PenalidadRechazarRequest req)
    {
        try { return Ok(await _service.RechazarResidenteAsync(id, req, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.RechazarResidente"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{id:int}/aprobar-gerencia")]
    [RequireFeature("ssoma.gestion.penalidades.aprobar-gerencia")]
    public async Task<IActionResult> AprobarGerencia(int id)
    {
        try { return Ok(await _service.AprobarGerenciaAsync(id, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.AprobarGerencia"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{id:int}/rechazar-gerencia")]
    [RequireFeature("ssoma.gestion.penalidades.aprobar-gerencia")]
    public async Task<IActionResult> RechazarGerencia(int id, [FromBody] PenalidadRechazarRequest req)
    {
        try { return Ok(await _service.RechazarGerenciaAsync(id, req, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.RechazarGerencia"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("{id:int}/documentos")]
    [RequestSizeLimit(20_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirDocumento(int id, [FromForm] IFormFile file)
    {
        try
        {
            var actual = await _service.GetDetalleAsync(id);
            if (actual is null) return NotFound(new { message = "Penalidad no encontrada." });
            if (!EsPropioDeContratista(actual.EmpresaId)) return Forbid();
            var url = await _service.SubirDocumentoDescargoAsync(id, file);
            return Ok(new { url });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.SubirDocumento"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{id:int}/descargo")]
    public async Task<IActionResult> PresentarDescargo(int id, [FromBody] PenalidadDescargaRequest req)
    {
        try
        {
            var actual = await _service.GetDetalleAsync(id);
            if (actual is null) return NotFound(new { message = "Penalidad no encontrada." });
            if (!EsPropioDeContratista(actual.EmpresaId)) return Forbid();
            await _service.PresentarDescargoAsync(id, req, GetUserId());
            return NoContent();
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.PresentarDescargo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{id:int}/evaluar-descargo")]
    [RequireFeature("ssoma.gestion.penalidades.evaluar")]
    public async Task<IActionResult> EvaluarDescargo(int id, [FromBody] PenalidadEvaluarDescargoRequest req)
    {
        try
        {
            if (EsContratista()) return Forbid();
            return Ok(await _service.EvaluarDescargoAsync(id, req, GetUserId()));
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.EvaluarDescargo"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{id:int}/decidir-gerencia")]
    [RequireFeature("ssoma.gestion.penalidades.aprobar-gerencia")]
    public async Task<IActionResult> DecidirGerencia(int id, [FromBody] PenalidadDecidirGerenciaRequest req)
    {
        try { return Ok(await _service.DecidirGerenciaAsync(id, req, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.DecidirGerencia"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{id:int}/apelar")]
    public async Task<IActionResult> Apelar(int id, [FromBody] PenalidadApelarRequest req)
    {
        try
        {
            var actual = await _service.GetDetalleAsync(id);
            if (actual is null) return NotFound(new { message = "Penalidad no encontrada." });
            if (!EsPropioDeContratista(actual.EmpresaId)) return Forbid();
            return Ok(await _service.ApelarAsync(id, req, GetUserId()));
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.Apelar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{id:int}/decidir-apelacion")]
    [RequireFeature("ssoma.gestion.penalidades.aprobar-gerencia")]
    public async Task<IActionResult> DecidirApelacion(int id, [FromBody] PenalidadDecidirApelacionRequest req)
    {
        try { return Ok(await _service.DecidirApelacionAsync(id, req, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.DecidirApelacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}/pdf-notificacion")]
    public async Task<IActionResult> GetPdfNotificacion(int id)
    {
        try
        {
            var actual = await _service.GetDetalleAsync(id);
            if (actual is null) return NotFound(new { message = "Penalidad no encontrada." });
            if (!EsPropioDeContratista(actual.EmpresaId)) return Forbid();
            return Redirect(await _service.GetPdfNotificacionAsync(id));
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.GetPdfNotificacion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}/pdf-resolucion")]
    public async Task<IActionResult> GetPdfResolucion(int id)
    {
        try
        {
            var actual = await _service.GetDetalleAsync(id);
            if (actual is null) return NotFound(new { message = "Penalidad no encontrada." });
            if (!EsPropioDeContratista(actual.EmpresaId)) return Forbid();
            return Redirect(await _service.GetPdfResolucionAsync(id));
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en PenalidadController.GetPdfResolucion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    /// <summary>
    /// Disparado por un cron externo (mismo patrón que AlertasController): recordatorios antes
    /// de vencer el plazo de descargo, y vencimientos que se dan por aceptados por incomparecencia.
    /// </summary>
    [HttpPost("cron/recordatorios")]
    [AllowAnonymous]
    public async Task<IActionResult> CronRecordatorios([FromHeader(Name = "Authorization")] string? authHeader)
    {
        if (authHeader != $"Bearer {_configuration["CronSecret"]}")
            return Unauthorized();

        try
        {
            await _service.ProcesarRecordatoriosYVencimientosAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en PenalidadController.CronRecordatorios");
            return StatusCode(500, new { message = "Error del servidor." });
        }
    }
}
