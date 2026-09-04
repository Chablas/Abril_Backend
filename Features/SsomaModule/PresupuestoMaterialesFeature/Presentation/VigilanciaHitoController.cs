using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Presentation;

/// <summary>
/// Servicio de vigilancia externa planificado por hito crítico del cronograma real del proyecto —
/// se factura por punto/turno cubierto (no por vigilante, eso es el rol interno VIGIA de
/// Dotación de personal). El precio unitario sale del ratio ya calculado en Ratios → Catálogo
/// para la família "Servicio de Vigilancia", el usuario solo decide cantidad de puntos y etapas.
/// </summary>
[ApiController]
[Route("api/v1/ssoma/presupuesto-materiales")]
[Authorize]
[RequireFeature("ssoma.gestion.presupuesto-materiales")]
public class VigilanciaHitoController : ControllerBase
{
    private readonly IVigilanciaHitoService _service;
    public VigilanciaHitoController(IVigilanciaHitoService service) => _service = service;

    private int UsuarioId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    /// <summary>Precio unitario vigente del servicio de vigilancia (promedio de los proyectos incluidos
    /// en Ratios · Precio para la família "Servicio de Vigilancia") — solo de referencia, el guardado
    /// vuelve a tomarlo en el momento real.</summary>
    [HttpGet("vigilancia/precio-actual")]
    public async Task<IActionResult> ObtenerPrecioActual()
    {
        try
        {
            var precio = await _service.ObtenerPrecioUnitarioActualAsync();
            return Ok(new { precioUnitario = precio ?? 0 });
        }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener el precio de vigilancia." }); }
    }

    [HttpGet("proyectos/{projectId}/vigilancia-hitos")]
    public async Task<IActionResult> Obtener(int projectId)
    {
        try
        {
            var filas = await _service.ObtenerPorProyectoAsync(projectId);
            return Ok(filas);
        }
        catch (Exception) { return StatusCode(500, new { message = "Error al obtener la vigilancia por hito." }); }
    }

    /// <summary>Reemplaza toda la vigilancia del proyecto por la lista enviada.</summary>
    [HttpPut("proyectos/{projectId}/vigilancia-hitos")]
    public async Task<IActionResult> Guardar(int projectId, [FromBody] VigilanciaHitoGuardarDto dto)
    {
        try
        {
            await _service.GuardarAsync(projectId, dto, UsuarioId);
            return Ok(new { message = "Vigilancia guardada correctamente." });
        }
        catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (Exception) { return StatusCode(500, new { message = "Error al guardar la vigilancia." }); }
    }
}
