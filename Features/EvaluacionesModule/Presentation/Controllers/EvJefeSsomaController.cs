using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Evaluaciones.Presentation.Controllers
{
    // Flujo B: el equipo SSOMA (Coordinador/Prevencionista) evalúa al Jefe SSOMA,
    // de forma anónima y obligatoria. La nota/comentario y la marca de "ya evaluó"
    // se guardan en tablas separadas sin FK entre sí (ver EvJefeSsomaRepository) —
    // ni siquiera un query directo a la base puede unir autor con respuesta.
    [ApiController]
    [Route("api/v1/evaluaciones/jefe-ssoma")]
    [Authorize]
    public class EvJefeSsomaController : ControllerBase
    {
        private readonly IEvJefeSsomaRepository _repo;
        private readonly IEvPeriodoRepository _periodoRepo;
        private readonly ILogger<EvJefeSsomaController> _logger;

        public EvJefeSsomaController(
            IEvJefeSsomaRepository repo,
            IEvPeriodoRepository periodoRepo,
            ILogger<EvJefeSsomaController> logger)
        {
            _repo = repo;
            _periodoRepo = periodoRepo;
            _logger = logger;
        }

        private int GetUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        /// <summary>
        /// Coordinador SSOMA/Prevencionista por PUESTO (workers.puesto_id -> puesto.categoria_id),
        /// no por un user_role aparte que hay que recordar asignar — ver
        /// EvGestionSsomaRepository para el mismo criterio.
        /// </summary>
        private async Task<bool> EsEquipoSsomaAsync(int userId)
        {
            var categoria = await _repo.ObtenerCategoriaPuestoAsync(userId);
            return categoria == CategoriaIds.CoordinadorSsoma || categoria == CategoriaIds.Prevencionista;
        }

        [HttpGet("inicio")]
        [Authorize]
        public async Task<IActionResult> GetInicio()
        {
            try
            {
                var userId = GetUserId();
                if (!await EsEquipoSsomaAsync(userId))
                    return StatusCode(403, new { message = "No tiene acceso a esta evaluación." });

                return Ok(await _repo.GetInicioAsync(userId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.GetInicio"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] EvJefeSsomaEvaluacionCreateDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (!await EsEquipoSsomaAsync(userId))
                    return StatusCode(403, new { message = "No tiene acceso a esta evaluación." });

                var periodo = await _periodoRepo.GetActivoAsync()
                    ?? throw new AbrilException("No hay período de evaluación activo.", 400);

                if (dto.Detalles.Count == 0)
                    throw new AbrilException("Debe calificar todos los criterios.", 400);

                if (dto.Detalles.Any(d => d.Puntaje is < 1 or > 5))
                    throw new AbrilException("El puntaje debe estar entre 1 y 5.", 400);

                var yaEvalue = await _repo.YaEvaluoAsync(periodo.Id, userId);
                if (yaEvalue)
                    throw new AbrilException("Ya evaluaste al Jefe SSOMA en este período.", 409);

                var puntajes = dto.Detalles.Select(d => d.Puntaje).ToList();
                var nota = Math.Round((decimal)puntajes.Average() * 4, 2);

                await _repo.RegistrarAsync(
                    periodo.Id, userId, dto.Comentario,
                    dto.Detalles.Select(d => (d.PlantillaId, d.Criterio, d.Puntaje)).ToList(),
                    nota);

                return StatusCode(201, new { message = "Evaluación registrada correctamente. Gracias por tu respuesta." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.Create"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("pendientes")]
        [Authorize]
        public async Task<IActionResult> GetPendientes([FromQuery] int? periodoId)
        {
            try
            {
                if (!await _repo.EsJefeSsomaPuestoAsync(GetUserId()))
                    return StatusCode(403, new { message = "No tiene acceso a esta pantalla." });

                var periodo = periodoId ?? (await _periodoRepo.GetActivoAsync())?.Id;
                if (periodo == null) return Ok(new EvJefeSsomaCumplimientoDto());
                return Ok(await _repo.GetCumplimientoAsync(periodo.Value));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.GetPendientes"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("resultados")]
        [Authorize]
        public async Task<IActionResult> GetResultados([FromQuery] int? periodoId)
        {
            try
            {
                if (!await _repo.EsJefeSsomaPuestoAsync(GetUserId()))
                    return StatusCode(403, new { message = "No tiene acceso a esta pantalla." });

                return Ok(await _repo.GetResultadosAsync(periodoId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.GetResultados"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // ─── Plan de acción (solo el Jefe SSOMA lo redacta/edita; se ve junto a Resultados) ──

        [HttpGet("plan-accion")]
        [Authorize]
        public async Task<IActionResult> GetPlanAccion([FromQuery] int periodoId)
        {
            try
            {
                if (!await _repo.EsJefeSsomaPuestoAsync(GetUserId()))
                    return StatusCode(403, new { message = "No tiene acceso a esta pantalla." });

                return Ok(await _repo.GetPlanAccionAsync(periodoId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.GetPlanAccion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("plan-accion")]
        [Authorize]
        public async Task<IActionResult> CrearPlanAccion([FromQuery] int periodoId, [FromBody] EvJefeSsomaPlanAccionCreateDto dto)
        {
            try
            {
                if (!await _repo.EsJefeSsomaPuestoAsync(GetUserId()))
                    return StatusCode(403, new { message = "No tiene acceso a esta pantalla." });

                if (string.IsNullOrWhiteSpace(dto.Accion) || string.IsNullOrWhiteSpace(dto.Meta))
                    throw new AbrilException("La acción y la meta medible son obligatorias.", 400);

                var creado = await _repo.CrearPlanAccionAsync(periodoId, GetUserId(), dto);
                return StatusCode(201, creado);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.CrearPlanAccion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPut("plan-accion/{id:int}")]
        [Authorize]
        public async Task<IActionResult> ActualizarPlanAccion(int id, [FromBody] EvJefeSsomaPlanAccionUpdateDto dto)
        {
            try
            {
                if (!await _repo.EsJefeSsomaPuestoAsync(GetUserId()))
                    return StatusCode(403, new { message = "No tiene acceso a esta pantalla." });

                if (string.IsNullOrWhiteSpace(dto.Accion) || string.IsNullOrWhiteSpace(dto.Meta))
                    throw new AbrilException("La acción y la meta medible son obligatorias.", 400);

                var actualizado = await _repo.ActualizarPlanAccionAsync(id, dto)
                    ?? throw new AbrilException("No se encontró el ítem del plan de acción.", 404);
                return Ok(actualizado);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.ActualizarPlanAccion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpDelete("plan-accion/{id:int}")]
        [Authorize]
        public async Task<IActionResult> EliminarPlanAccion(int id)
        {
            try
            {
                if (!await _repo.EsJefeSsomaPuestoAsync(GetUserId()))
                    return StatusCode(403, new { message = "No tiene acceso a esta pantalla." });

                var eliminado = await _repo.EliminarPlanAccionAsync(id);
                if (!eliminado) throw new AbrilException("No se encontró el ítem del plan de acción.", 404);
                return NoContent();
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.EliminarPlanAccion"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
