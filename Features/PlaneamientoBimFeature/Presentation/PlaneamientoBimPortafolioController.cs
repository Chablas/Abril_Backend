using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Presentation
{
    /// <summary>Landing de Planeamiento BIM (antes de elegir proyecto) — KPIs agregados de
    /// todo el portafolio. Rol más restrictivo que el resto del feature a propósito: es
    /// vista de solo lectura para AdministradorSistema/AdministradorUdp, sin UsuarioUdp
    /// (no hay rol "Gerencia/Dirección" en la tabla role — decisión confirmada con el usuario).</summary>
    [ApiController]
    [Route("api/v1/planeamiento-bim/portafolio")]
    [Authorize(Roles = $"{Roles.AdministradorSistema},{Roles.AdministradorUdp}")]
    public class PlaneamientoBimPortafolioController : ControllerBase
    {
        private readonly IPlaneamientoBimPortafolioService _service;

        public PlaneamientoBimPortafolioController(IPlaneamientoBimPortafolioService service)
        {
            _service = service;
        }

        [HttpGet("kpis")]
        public async Task<IActionResult> GetKpis()
        {
            try
            {
                return Ok(await _service.GetKpis());
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("proyectos")]
        public async Task<IActionResult> GetProyectos()
        {
            try
            {
                return Ok(await _service.GetProyectos());
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("{projectId:int}/export-pdf")]
        public async Task<IActionResult> ExportarPdf(int projectId, [FromQuery] DateOnly fecha)
        {
            try
            {
                var pdf = await _service.ExportarPdf(projectId, fecha);
                return File(pdf, "application/pdf", $"ProgramacionDiaria_{projectId}_{fecha:yyyyMMdd}.pdf");
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
