using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Presentation;

/// <summary>
/// Servicios de costo fijo (VariableBase = FIJO en Catálogo: alquileres, letreros, etc.) — no
/// escalan con HH/Área/Trabajadores como los materiales, así que la cantidad se tipea manualmente
/// por proyecto. El precio unitario sigue viniendo de Ratios (mismo mecanismo que Vigilancia).
/// </summary>
[ApiController]
[Route("api/v1/ssoma/presupuesto-materiales/proyectos/{projectId}/servicios")]
[Authorize]
[RequireFeature("ssoma.gestion.presupuesto-materiales")]
public class ServicioFijoController : ControllerBase
{
    private readonly IServicioFijoService _service;
    public ServicioFijoController(IServicioFijoService service) => _service = service;

    private int UsuarioId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>Familias de Catálogo con VariableBase = FIJO, para el selector de "agregar servicio".</summary>
    [HttpGet("disponibles")]
    public async Task<IActionResult> ObtenerDisponibles()
    {
        try { return Ok(await _service.ObtenerFamiliasFijasDisponiblesAsync()); }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener los servicios disponibles." }); }
    }

    [HttpGet]
    public async Task<IActionResult> Obtener(int projectId)
    {
        try { return Ok(await _service.ObtenerPorProyectoAsync(projectId)); }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener los servicios del proyecto." }); }
    }

    /// <summary>Reemplaza todos los servicios fijos del proyecto por la lista enviada.</summary>
    [HttpPut]
    public async Task<IActionResult> Guardar(int projectId, [FromBody] ServiciosFijosGuardarDto dto)
    {
        try
        {
            await _service.GuardarAsync(projectId, dto, UsuarioId);
            return Ok(new { message = "Servicios guardados correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al guardar los servicios." }); }
    }
}
