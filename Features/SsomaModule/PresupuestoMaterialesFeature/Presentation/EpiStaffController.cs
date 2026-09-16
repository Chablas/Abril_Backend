using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Presentation;

/// <summary>Cálculo de EPI de Staff (Casco/Orejera/Arnés/Lentes/Barbiquejo/Guantes) por proyecto,
/// y la configuración global de rotación (una sola fila, para toda la empresa) que lo alimenta.</summary>
[ApiController]
[Route("api/v1/ssoma/presupuesto-materiales/epi-staff")]
[Authorize]
[RequireFeature("ssoma.gestion.presupuesto-materiales")]
public class EpiStaffController : ControllerBase
{
    private readonly IEpiStaffCalculoService _service;
    private readonly ILogger<EpiStaffController> _logger;

    public EpiStaffController(IEpiStaffCalculoService service, ILogger<EpiStaffController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Config global de rotación (arnés por staff, y cada cuántos meses se repone
    /// lentes/barbiquejo/guantes) — una sola fila para toda la empresa.</summary>
    [HttpGet("config")]
    public async Task<IActionResult> ObtenerConfig()
    {
        try { return Ok(await _service.ObtenerConfigAsync()); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la configuración de EPI Staff");
            return StatusCode(500, new { message = "Error al obtener la configuración." });
        }
    }

    [HttpPut("config")]
    public async Task<IActionResult> ActualizarConfig([FromBody] ActualizarEpiStaffConfigDto dto)
    {
        try
        {
            await _service.ActualizarConfigAsync(dto);
            return Ok(new { message = "Configuración actualizada." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar la configuración de EPI Staff");
            return StatusCode(500, new { message = "Error al actualizar la configuración." });
        }
    }

    /// <summary>Cálculo completo para un proyecto: StaffHeadcount aplicado, meses de proyecto, y
    /// el detalle Staff/Obrero de cada ítem de EPI compartido.</summary>
    [HttpGet("proyectos/{projectId}/calculo")]
    public async Task<IActionResult> Calcular(int projectId)
    {
        try
        {
            var resultado = await _service.CalcularAsync(projectId);
            if (resultado is null) return NotFound(new { message = "El proyecto todavía no tiene ningún presupuesto generado." });
            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al calcular EPI Staff del proyecto {ProjectId}", projectId);
            return StatusCode(500, new { message = "Error al calcular EPI Staff." });
        }
    }
}
