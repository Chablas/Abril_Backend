using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.SsomaModule.AmonestacionesFeature.Presentation;

[ApiController]
[Route("api/v1/ssoma-amonestaciones")]
[Authorize]
[RequireFeature("ssoma.gestion.amonestaciones")]
public class AmonestacionController : ControllerBase
{
    private readonly IAmonestacionService _service;
    private readonly ILogger<AmonestacionController> _logger;

    public AmonestacionController(IAmonestacionService service, ILogger<AmonestacionController> logger)
    {
        _service = service;
        _logger  = logger;
    }

    private int GetUserId() =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    private bool EsContratista() => User.FindFirst("tipo")?.Value == "CONTRATISTA";

    private int? GetEmpresaIdContratista() =>
        EsContratista() && int.TryParse(User.FindFirst("empresaId")?.Value, out var id) ? id : null;

    /// <summary>Contratista solo puede ver/operar registros de trabajadores de su propia empresa.</summary>
    private bool EsPropioDeContratista(int? empresaId)
    {
        var propia = GetEmpresaIdContratista();
        return !propia.HasValue || empresaId == propia.Value;
    }

    [HttpGet("init")]
    public async Task<IActionResult> GetInit()
    {
        try { return Ok(await _service.GetInitAsync()); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en AmonestacionController.GetInit"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] AmonestacionCreateRequest req)
    {
        try
        {
            var empresaId = GetEmpresaIdContratista();
            if (empresaId.HasValue)
            {
                var puntaje = await _service.GetPuntajeWorkerAsync(req.WorkerId);
                if (puntaje is null || !EsPropioDeContratista(puntaje.EmpresaId))
                    return Forbid();
            }
            return StatusCode(201, await _service.CrearAsync(req, GetUserId()));
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en AmonestacionController.Crear"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] AmonestacionListQuery q)
    {
        try
        {
            q.EmpresaIdContratista = GetEmpresaIdContratista();
            return Ok(await _service.GetListAsync(q));
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en AmonestacionController.GetList"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try { return Ok(await _service.GetDashboardAsync(GetEmpresaIdContratista())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en AmonestacionController.GetDashboard"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        try
        {
            var r = await _service.GetDetalleAsync(id);
            if (r is null) return NotFound(new { message = "Amonestación no encontrada." });
            if (!EsPropioDeContratista(r.EmpresaId)) return Forbid();
            return Ok(r);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en AmonestacionController.GetDetalle"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("worker/{workerId:int}/puntaje")]
    public async Task<IActionResult> GetPuntajeWorker(int workerId)
    {
        try
        {
            var r = await _service.GetPuntajeWorkerAsync(workerId);
            if (r is null) return NotFound(new { message = "Trabajador no encontrado." });
            if (!EsPropioDeContratista(r.EmpresaId)) return Forbid();
            return Ok(r);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en AmonestacionController.GetPuntajeWorker"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("{id:int}/confirmar")]
    public async Task<IActionResult> Confirmar(int id)
    {
        try
        {
            var actual = await _service.GetDetalleAsync(id);
            if (actual is null) return NotFound(new { message = "Amonestación no encontrada." });
            if (!EsPropioDeContratista(actual.EmpresaId)) return Forbid();
            return Ok(await _service.ConfirmarAsync(id));
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en AmonestacionController.Confirmar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        try
        {
            var actual = await _service.GetDetalleAsync(id);
            if (actual is null) return NotFound(new { message = "Amonestación no encontrada." });
            if (!EsPropioDeContratista(actual.EmpresaId)) return Forbid();
            var result = await _service.GetPdfAsync(id);
            if (result.RedirectUrl != null)
                return Redirect(result.RedirectUrl);
            return File(result.Bytes!, "application/pdf", $"Amonestacion-{id}.pdf");
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en AmonestacionController.GetPdf"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("{id:int}/cerrar")]
    public async Task<IActionResult> Cerrar(int id, [FromBody] AmonestacionCerrarRequest req)
    {
        try
        {
            var actual = await _service.GetDetalleAsync(id);
            if (actual is null) return NotFound(new { message = "Amonestación no encontrada." });
            if (!EsPropioDeContratista(actual.EmpresaId)) return Forbid();
            await _service.CerrarAsync(id, req);
            return Ok(new { message = "Amonestación cerrada correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en AmonestacionController.Cerrar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }
}
