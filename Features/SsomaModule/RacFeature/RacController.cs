using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Ssoma.Rac.Dtos;
using Abril_Backend.Features.Ssoma.Rac.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Ssoma.Rac;

[ApiController]
[Route("api/v1/ssoma-rac")]
[AllowAnonymous]
public class RacController : ControllerBase
{
    private readonly IRacService _service;
    private readonly ILogger<RacController> _logger;

    public RacController(IRacService service, ILogger<RacController> logger)
    {
        _service = service;
        _logger = logger;
    }

    private int GetUserId() =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] RacListQuery q)
    {
        try { return Ok(await _service.GetListAsync(q)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.GetList"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        try { return Ok(await _service.GetDashboardAsync()); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.GetDashboard"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("categorias")]
    public async Task<IActionResult> GetCategorias()
    {
        try { return Ok(await _service.GetCategoriasAsync()); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.GetCategorias"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("infracciones")]
    public async Task<IActionResult> GetInfracciones()
    {
        try { return Ok(await _service.GetInfraccionesAsync()); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.GetInfracciones"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("proyecto/{projectId:int}/niveles")]
    public async Task<IActionResult> GetNiveles(int projectId)
    {
        try { return Ok(await _service.GetNivelesProyectoAsync(projectId)); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.GetNiveles"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] RacCreateRequest req)
    {
        try { return StatusCode(201, await _service.CrearAsync(req, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.Crear"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetalle(int id)
    {
        try
        {
            var r = await _service.GetDetalleAsync(id);
            return r is null ? NotFound(new { message = "RAC no encontrado." }) : Ok(r);
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.GetDetalle"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPatch("{id:int}/cerrar")]
    public async Task<IActionResult> Cerrar(int id, [FromBody] RacCerrarRequest req)
    {
        try { return Ok(await _service.CerrarAsync(id, req, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.Cerrar"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpPost("{id:int}/fotos")]
    [RequestSizeLimit(50_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubirFoto(int id, [FromForm] IFormFile file, [FromForm] string tipo = "Hallazgo")
    {
        try { return StatusCode(201, await _service.SubirFotoAsync(id, file, tipo, GetUserId())); }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.SubirFoto"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}/reporte")]
    public async Task<IActionResult> GetReporte(int id)
    {
        try
        {
            var rac = await _service.GetDetalleAsync(id);
            if (rac is null) return NotFound(new { message = "RAC no encontrado." });
            if (string.IsNullOrWhiteSpace(rac.PdfUrl)) return NotFound(new { message = "PDF no disponible." });
            var url = "https://abrilinmob.sharepoint.com/sites/SSOMA-Powerapps/RacPDF2026/" + rac.PdfUrl;
            return Ok(new { url });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.GetReporte"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> GetPdf(int id)
    {
        try
        {
            var bytes = await _service.GetPdfAsync(id);
            return File(bytes, "application/pdf", $"RAC-{id}.pdf");
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "Error en RacController.GetPdf"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
    }
}
