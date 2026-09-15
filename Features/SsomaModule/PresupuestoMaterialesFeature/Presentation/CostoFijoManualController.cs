using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Presentation;

/// <summary>Montos fijos "glb" tipeados a mano por proyecto — Malla Anticaída, Encapsulado y
/// Malla Anillo Fenólico (no escalan con ningún driver ni tienen ratio histórico confiable).</summary>
[ApiController]
[Route("api/v1/ssoma/presupuesto-materiales/proyectos/{projectId}/costo-fijo-manual")]
[Authorize]
[RequireFeature("ssoma.gestion.presupuesto-materiales")]
public class CostoFijoManualController : ControllerBase
{
    private readonly ICostoFijoManualService _service;
    private readonly ILogger<CostoFijoManualController> _logger;

    public CostoFijoManualController(ICostoFijoManualService service, ILogger<CostoFijoManualController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Obtener(int projectId)
    {
        try { return Ok(await _service.ObtenerPorProyectoAsync(projectId)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener el costo fijo manual del proyecto {ProjectId}", projectId);
            return StatusCode(500, new { message = "Error al obtener el costo fijo manual." });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Guardar(int projectId, [FromBody] ActualizarCostoFijoManualDto dto)
    {
        try
        {
            await _service.GuardarAsync(projectId, dto);
            return Ok(new { message = "Costo fijo manual guardado." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al guardar el costo fijo manual del proyecto {ProjectId}", projectId);
            return StatusCode(500, new { message = "Error al guardar el costo fijo manual." });
        }
    }
}
