using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.HabEmpresa;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Habilitacion.Presentation
{
    [ApiController]
    [Route("api/v1/habilitacion/empresas/{empresaId:int}/entregables")]
    [Authorize]
    public class HabEmpresaController : ControllerBase
    {
        private static readonly string[] RolesAprobadores = ["ADMINISTRADOR SSOMA", "ADMINISTRADOR DE UDP", "ADMINISTRADOR ADMINISTRACION"];

        private readonly IHabEmpresaRepository _repo;
        private readonly ILogger<HabEmpresaController> _logger;

        public HabEmpresaController(IHabEmpresaRepository repo, ILogger<HabEmpresaController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetEntregables(
            int empresaId,
            [FromQuery] int proyectoId,
            [FromQuery] int? mes,
            [FromQuery] int? anio)
        {
            try
            {
                var items = await _repo.GetEntregablesEmpresaAsync(empresaId, proyectoId, mes, anio);
                return Ok(items);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabEmpresaController.GetEntregables"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateEntregable(int empresaId, int id, [FromBody] EmpresaEntregableUpdateDto dto)
        {
            try
            {
                var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
                var esContratista = roles.Any(r => r.Equals("CONTRATISTA", StringComparison.OrdinalIgnoreCase));
                var esAprobador = roles.Any(r => RolesAprobadores.Contains(r, StringComparer.OrdinalIgnoreCase));

                if (esContratista && !string.Equals(dto.Estado, "Enviado", StringComparison.OrdinalIgnoreCase))
                    return StatusCode(403, new { message = "Los contratistas solo pueden enviar entregables; la aprobación es interna." });

                if ((string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(dto.Estado, "Rechazado", StringComparison.OrdinalIgnoreCase))
                    && !esAprobador)
                    return StatusCode(403, new { message = "Solo ADMINISTRADOR SSOMA o ADMINISTRADOR DE UDP pueden aprobar o rechazar." });

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                int? userId = userIdClaim != null && int.TryParse(userIdClaim.Value, out var uid) ? uid : null;

                int? empresaIdClaim = null;
                if (esContratista)
                {
                    var ec = User.FindFirst("empresaId")?.Value;
                    if (int.TryParse(ec, out var eid)) empresaIdClaim = eid;
                    userId = null;
                }

                var actualizado = await _repo.UpdateEntregableEmpresaAsync(id, dto, userId, empresaIdClaim);
                return Ok(actualizado);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabEmpresaController.UpdateEntregable"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
