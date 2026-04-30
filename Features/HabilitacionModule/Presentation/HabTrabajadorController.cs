using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application.Dtos.Trabajadores;
using Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Habilitacion.Presentation
{
    [ApiController]
    [Route("api/v1/habilitacion/trabajadores")]
    [Authorize]
    public class HabTrabajadorController : ControllerBase
    {
        private static readonly string[] RolesAprobadores = ["ADMINISTRADOR SSOMA", "ADMINISTRADOR DE UDP", "ADMINISTRADOR ADMINISTRACION"];

        private readonly IHabTrabajadorRepository _repo;
        private readonly ILogger<HabTrabajadorController> _logger;

        public HabTrabajadorController(IHabTrabajadorRepository repo, ILogger<HabTrabajadorController> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public class ReingresoRequest
        {
            public int ProyectoId { get; set; }
            public int EmpresaId { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkers(
            [FromQuery] string? search,
            [FromQuery] int? empresaId,
            [FromQuery] int? proyectoId,
            [FromQuery] string? estadoHabilitacion,
            [FromQuery] string? contratistaCasa,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
                if (roles.Any(r => r.Equals("CONTRATISTA", StringComparison.OrdinalIgnoreCase)))
                {
                    if (!int.TryParse(User.FindFirst("empresaId")?.Value, out var empresaJwt))
                        return StatusCode(403, new { message = "Token de contratista inválido." });
                    empresaId = empresaJwt;
                }

                var (items, total) = await _repo.GetWorkersHabilitacionAsync(
                    search, empresaId, proyectoId, estadoHabilitacion, contratistaCasa, page, pageSize);

                var result = new PagedResult<WorkerHabilitacionListDto>
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalRecords = total,
                    TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                    Data = items
                };

                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.GetWorkers"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var worker = await _repo.GetByIdAsync(id);
                if (worker is null)
                    return NotFound(new { message = "Trabajador no encontrado." });
                return Ok(worker);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.GetById"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] WorkerUpdateDto dto)
        {
            try
            {
                var actualizado = await _repo.UpdateAsync(id, dto);
                return Ok(actualizado);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.Update"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("{workerId:int}/entregables")]
        public async Task<IActionResult> GetEntregables(int workerId)
        {
            try
            {
                var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
                if (roles.Any(r => r.Equals("CONTRATISTA", StringComparison.OrdinalIgnoreCase)))
                {
                    var empresaClaim = User.FindFirst("empresaId")?.Value;
                    if (!int.TryParse(empresaClaim, out var empresaJwt))
                        return StatusCode(403, new { message = "Token de contratista inválido." });

                    var empresaActiva = await _repo.GetEmpresaActivaWorkerAsync(workerId);
                    if (empresaActiva is null || empresaActiva.Value != empresaJwt)
                        return StatusCode(403, new { message = "Este trabajador no pertenece a su empresa." });
                }

                return Ok(await _repo.GetEntregablesWorkerAsync(workerId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.GetEntregables"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("entregables/{id:int}")]
        public async Task<IActionResult> UpdateEntregable(int id, [FromBody] WorkerEntregableUpdateDto dto)
        {
            try
            {
                var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
                var esContratista = roles.Any(r => r.Equals("CONTRATISTA", StringComparison.OrdinalIgnoreCase));
                var esAprobador = roles.Any(r => RolesAprobadores.Contains(r, StringComparer.OrdinalIgnoreCase));

                if (esContratista && !string.Equals(dto.Estado, "Enviado", StringComparison.OrdinalIgnoreCase))
                    return StatusCode(403, new { message = "Los contratistas solo pueden enviar entregables; la aprobación es interna." });

                if (string.Equals(dto.Estado, "Aprobado", StringComparison.OrdinalIgnoreCase) && !esAprobador)
                    return StatusCode(403, new { message = "Solo ADMINISTRADOR SSOMA o ADMINISTRADOR DE UDP pueden aprobar." });

                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                int? userId = userIdClaim != null && int.TryParse(userIdClaim.Value, out var uid) ? uid : null;

                int? empresaIdClaim = null;
                if (esContratista)
                {
                    var ec = User.FindFirst("empresaId")?.Value;
                    if (int.TryParse(ec, out var eid)) empresaIdClaim = eid;
                    userId = null;
                }

                var actualizado = await _repo.UpdateEntregableAsync(id, dto, userId, empresaIdClaim);
                return Ok(actualizado);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.UpdateEntregable"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("entregables/{id:int}/versiones")]
        public async Task<IActionResult> GetVersionesDocumento(int id)
        {
            try
            {
                var versiones = await _repo.GetVersionesDocumentoAsync(id);
                return Ok(versiones);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.GetVersionesDocumento"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("{workerId:int}/cambiar-obra")]
        public async Task<IActionResult> CambiarObra(int workerId, [FromBody] WorkerCambiarObraDto dto)
        {
            try
            {
                await _repo.CambiarObraAsync(workerId, dto);
                return Ok(new { message = "Cambio de obra registrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.CambiarObra"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{workerId:int}/inicializar")]
        public async Task<IActionResult> InicializarEntregables(int workerId)
        {
            try
            {
                await _repo.InicializarEntregablesAsync(workerId);
                return Ok(new { message = "Entregables inicializados correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.InicializarEntregables"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("{workerId:int}/reingreso")]
        public async Task<IActionResult> Reingreso(int workerId, [FromBody] ReingresoRequest body)
        {
            try
            {
                await _repo.ReingresoAsync(workerId, body.ProyectoId, body.EmpresaId);
                return Ok(new { message = "Reingreso registrado." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.Reingreso"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
