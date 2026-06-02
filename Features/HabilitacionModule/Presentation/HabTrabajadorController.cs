using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Habilitacion.Application;
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


        [HttpGet]
        public async Task<IActionResult> GetWorkers(
            [FromQuery] string? search,
            [FromQuery] int? empresaId,
            [FromQuery] int? proyectoId,
            [FromQuery] string? estadoHabilitacion,
            [FromQuery] string? contratistaCasa,
            [FromQuery] bool soloRetirados = false,
            [FromQuery] bool soloVerificacion = false,
            [FromQuery] bool soloSinEmo = false,
            [FromQuery] bool soloEmoVencido = false,
            [FromQuery] bool soloSinVidaLey = false,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                if (User.FindFirst("tipo")?.Value == "CONTRATISTA" && !soloVerificacion)
                {
                    if (!int.TryParse(User.FindFirst("empresaId")?.Value, out var empresaJwt))
                        return StatusCode(403, new { message = "Token de contratista inválido." });
                    empresaId = empresaJwt;
                }

                var proyectosFiltro = ContratistaProyectosHelper.GetProyectosFiltro(User);
                if (proyectosFiltro != null && proyectoId == null)
                {
                    if (proyectosFiltro.Count == 1)
                        proyectoId = proyectosFiltro[0];
                    else if (proyectosFiltro.Count == 0)
                        return Ok(new PagedResult<object> { Data = new() });
                    // Si tiene múltiples proyectos, por ahora no filtrar — se implementa después
                }

                var (items, total) = await _repo.GetWorkersHabilitacionAsync(
                    search, empresaId, proyectoId, estadoHabilitacion, contratistaCasa, page, pageSize, soloRetirados, soloSinEmo, soloEmoVencido, soloSinVidaLey);

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
                if (User.FindFirst("tipo")?.Value == "CONTRATISTA")
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
                var esContratista = User.FindFirst("tipo")?.Value == "CONTRATISTA";
                var esAprobador = User.FindAll(ClaimTypes.Role).Any(c => RolesAprobadores.Contains(c.Value, StringComparer.OrdinalIgnoreCase));

                if (esContratista)
                {
                    dto.Estado = "Enviado";
                    dto.Vigencia = null;
                }

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
        public async Task<IActionResult> Reingreso(int workerId, [FromBody] WorkerReingresoDto dto)
        {
            try
            {
                await _repo.ReingresoAsync(workerId, dto);
                return Ok(new { message = "Trabajador reingresado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.Reingreso"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("baja-masiva")]
        public async Task<IActionResult> BajaMasiva([FromBody] WorkerBajaMasivaDto dto)
        {
            try
            {
                if (dto.Ids == null || dto.Ids.Count == 0)
                    return BadRequest(new { message = "Debe proporcionar al menos un ID de trabajador." });

                var fechaRetiro = dto.FechaRetiro ?? DateOnly.FromDateTime(DateTime.UtcNow);
                await _repo.BajaMasivaAsync(dto.Ids, fechaRetiro);
                return Ok(new { message = $"{dto.Ids.Count} trabajador(es) dado(s) de baja correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.BajaMasiva"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("{workerId:int}/baja")]
        public async Task<IActionResult> Baja(int workerId, [FromBody] WorkerBajaDto dto)
        {
            try
            {
                var fechaRetiro = dto.FechaRetiro ?? DateOnly.FromDateTime(DateTime.UtcNow);
                await _repo.BajaAsync(workerId, fechaRetiro);
                return Ok(new { message = "Trabajador dado de baja correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.Baja"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("{workerId:int}/eventos")]
        public async Task<IActionResult> GetEventos(int workerId)
        {
            try
            {
                var eventos = await _repo.GetEventosAsync(workerId);
                return Ok(eventos);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.GetEventos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("{workerId:int}/proyectos")]
        public async Task<IActionResult> AgregarProyecto(int workerId, [FromBody] AgregarProyectoDto dto)
        {
            try
            {
                var creado = await _repo.AgregarProyectoAsync(workerId, dto);
                return Ok(creado);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.AgregarProyecto"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("{workerId:int}/proyectos")]
        public async Task<IActionResult> GetProyectos(int workerId)
        {
            try
            {
                var proyectos = await _repo.GetProyectosAsync(workerId);
                return Ok(proyectos);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.GetProyectos"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("{workerId:int}/proyectos/{proyectoId:int}")]
        public async Task<IActionResult> RetirarDeProyecto(int workerId, int proyectoId)
        {
            try
            {
                await _repo.RetirarDeProyectoAsync(workerId, proyectoId);
                return Ok(new { message = "Trabajador retirado del proyecto correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.RetirarDeProyecto"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPatch("{workerId:int}/proyectos/{proyectoId:int}/induccion")]
        public async Task<IActionResult> MarcarInduccion(int workerId, int proyectoId)
        {
            try
            {
                await _repo.MarcarInduccionAsync(workerId, proyectoId);
                return Ok(new { message = "Inducción marcada como completada." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.MarcarInduccion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("reparar-vinculaciones")]
        public async Task<IActionResult> RepararVinculaciones()
        {
            if (!User.Claims.Any(c => c.Type == ClaimTypes.Role && RolesAprobadores.Contains(c.Value)))
                return StatusCode(403, new { message = "Solo administradores pueden ejecutar esta operación." });
            try
            {
                var reparados = await _repo.RepararVinculacionesAsync();
                return Ok(new
                {
                    total = reparados.Count,
                    message = reparados.Count == 0
                        ? "No se encontraron workers con vinculaciones inconsistentes."
                        : $"{reparados.Count} worker(s) reparados.",
                    workers = reparados,
                });
            }
            catch (Exception ex) { _logger.LogError(ex, "Error en HabTrabajadorController.RepararVinculaciones"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
