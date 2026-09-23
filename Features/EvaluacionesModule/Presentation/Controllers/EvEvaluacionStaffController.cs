using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Evaluaciones.Presentation.Controllers
{
    // Evaluación 360° de Staff: el Residente de cada proyecto evalúa, de forma
    // IDENTIFICADA, a todo el staff de SU proyecto (puestos en
    // Shared.Constants.PuestoIds.StaffEvaluablePuestoIds). A diferencia de
    // EvJefeSsomaController/EvGestionSsomaController, acá SÍ se sabe qué
    // Residente evaluó a qué trabajador con qué nota — no hay anonimato.
    [ApiController]
    [Route("api/v1/evaluaciones/staff")]
    [Authorize]
    public class EvEvaluacionStaffController : ControllerBase
    {
        private readonly IEvEvaluacionStaffRepository _repo;
        private readonly IEvPeriodoRepository _periodoRepo;
        private readonly ILogger<EvEvaluacionStaffController> _logger;

        public EvEvaluacionStaffController(
            IEvEvaluacionStaffRepository repo,
            IEvPeriodoRepository periodoRepo,
            ILogger<EvEvaluacionStaffController> logger)
        {
            _repo = repo;
            _periodoRepo = periodoRepo;
            _logger = logger;
        }

        private int GetUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        [HttpGet("pendientes")]
        [Authorize]
        public async Task<IActionResult> GetPendientes()
        {
            try
            {
                var userId = GetUserId();
                if (!await _repo.EsResidenteAsync(userId))
                    return StatusCode(403, new { message = "No tiene acceso a esta evaluación." });

                var periodo = await _periodoRepo.GetActivoAsync();
                if (periodo == null) return Ok(new List<EvEvaluacionStaffPendienteDto>());

                return Ok(await _repo.GetPendientesAsync(userId, periodo.Id));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvEvaluacionStaffController.GetPendientes"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("plantilla/{puestoId:int}")]
        [Authorize]
        public async Task<IActionResult> GetPlantilla(int puestoId)
        {
            try
            {
                if (!await _repo.EsResidenteAsync(GetUserId()))
                    return StatusCode(403, new { message = "No tiene acceso a esta evaluación." });

                return Ok(await _repo.GetPlantillaPorPuestoAsync(puestoId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvEvaluacionStaffController.GetPlantilla"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] EvEvaluacionStaffCreateDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (!await _repo.EsResidenteAsync(userId))
                    return StatusCode(403, new { message = "No tiene acceso a esta evaluación." });

                var periodo = await _periodoRepo.GetActivoAsync()
                    ?? throw new AbrilException("No hay período de evaluación activo.", 400);

                var proyectoId = await _repo.ObtenerProyectoDeResidenteAsync(userId)
                    ?? throw new AbrilException("No se pudo determinar el proyecto del residente.", 400);

                if (dto.Detalles.Count == 0)
                    throw new AbrilException("Debe calificar todos los criterios.", 400);

                if (dto.Detalles.Any(d => d.Puntaje is < 1 or > 5))
                    throw new AbrilException("El puntaje debe estar entre 1 y 5.", 400);

                var puestoEvaluado = await _repo.ValidarEvaluadoAsync(dto.EvaluadoWorkerId, proyectoId)
                    ?? throw new AbrilException("El trabajador indicado no es evaluable en su proyecto.", 400);
                _ = puestoEvaluado;

                var yaEvaluo = await _repo.YaEvaluoAsync(periodo.Id, userId, dto.EvaluadoWorkerId);
                if (yaEvaluo)
                    throw new AbrilException("Ya evaluaste a este trabajador en este período.", 409);

                var puntajes = dto.Detalles.Select(d => d.Puntaje).ToList();
                var nota = Math.Round((decimal)puntajes.Average() * 4, 2);

                await _repo.CreateAsync(
                    periodo.Id, userId, dto.EvaluadoWorkerId, proyectoId,
                    dto.Comentario,
                    dto.Detalles.Select(d => (d.PlantillaId, d.Criterio, d.Puntaje)).ToList(),
                    nota);

                return StatusCode(201, new { message = "Evaluación registrada correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvEvaluacionStaffController.Create"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("resultados")]
        [Authorize]
        public async Task<IActionResult> GetResultados([FromQuery] int? periodoId, [FromQuery] int? projectId, [FromQuery] int? puestoId)
        {
            try
            {
                var userId = GetUserId();
                if (!await _repo.EsResidenteAsync(userId))
                    return StatusCode(403, new { message = "No tiene acceso a esta pantalla." });

                var periodo = periodoId ?? (await _periodoRepo.GetActivoAsync())?.Id;
                if (periodo == null) return Ok(new List<EvEvaluacionStaffResultadoDto>());

                return Ok(await _repo.GetResultadosAsync(periodo.Value, projectId, puestoId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvEvaluacionStaffController.GetResultados"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
