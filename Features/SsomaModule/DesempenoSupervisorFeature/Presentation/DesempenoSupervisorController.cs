using Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.SsomaModule.DesempenoSupervisorFeature.Presentation;

[ApiController]
[Route("api/v1/ssoma-desempeno-supervisor")]
[Authorize]
[RequireFeature("ssoma.gestion.indicadores-proactivos")]
public class DesempenoSupervisorController(DesempenoSupervisorRepository repo) : ControllerBase
{
    private int GetUserId() =>
        int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    /// <summary>
    /// Quién puede VER y USAR el botón de ocultar/mostrar (aplica a todas las tarjetas
    /// por igual) — Coordinadores SSOMA, administradores del sistema, o una excepción
    /// puntual válida solo mientras se opera sobre un proyecto específico (ver
    /// <see cref="DesempenoSupervisorRepository.EsCoordinadorSsomaAsync"/>). No es una
    /// restricción de a quién se puede ocultar, es un permiso de quién puede hacerlo.
    /// </summary>
    private async Task<bool> PuedeOcultarAsync(int? proyectoId)
    {
        if (User.IsInRole(Roles.AdministradorSistema)) return true;
        return await repo.EsCoordinadorSsomaAsync(GetUserId(), proyectoId);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int mes, [FromQuery] int anio, [FromQuery] int? proyectoId, [FromQuery] bool incluirOcultos = false)
    {
        if (mes < 1 || mes > 12 || anio < 2020)
            return BadRequest("Mes o año inválido.");
        try
        {
            var puedeOcultar = await PuedeOcultarAsync(proyectoId);
            var result = await repo.GetDesempenoAsync(mes, anio, proyectoId, incluirOcultos, puedeOcultar);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    [HttpPatch("{workerId:int}/ocultar")]
    public async Task<IActionResult> Ocultar(int workerId, [FromQuery] int? proyectoId, [FromBody] OcultarSupervisorRequest? req)
    {
        try
        {
            if (!await PuedeOcultarAsync(proyectoId)) return Forbid();
            await repo.OcultarAsync(workerId, req?.Motivo, GetUserId());
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPatch("{workerId:int}/mostrar")]
    public async Task<IActionResult> Mostrar(int workerId, [FromQuery] int? proyectoId)
    {
        try
        {
            if (!await PuedeOcultarAsync(proyectoId)) return Forbid();
            await repo.MostrarAsync(workerId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}

public class OcultarSupervisorRequest
{
    public string? Motivo { get; set; }
}
