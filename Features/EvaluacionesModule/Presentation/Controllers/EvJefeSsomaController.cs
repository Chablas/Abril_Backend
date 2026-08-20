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

        private const string RolesEvaluador = $"{Roles.CoordinadorSsoma},{Roles.Prevencionista}";

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

        [HttpGet("inicio")]
        [Authorize(Roles = RolesEvaluador)]
        public async Task<IActionResult> GetInicio()
        {
            try { return Ok(await _repo.GetInicioAsync(GetUserId())); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.GetInicio"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        [Authorize(Roles = RolesEvaluador)]
        public async Task<IActionResult> Create([FromBody] EvJefeSsomaEvaluacionCreateDto dto)
        {
            try
            {
                var periodo = await _periodoRepo.GetActivoAsync()
                    ?? throw new AbrilException("No hay período de evaluación activo.", 400);

                if (dto.Detalles.Count == 0)
                    throw new AbrilException("Debe calificar todos los criterios.", 400);

                if (dto.Detalles.Any(d => d.Puntaje is < 1 or > 5))
                    throw new AbrilException("El puntaje debe estar entre 1 y 5.", 400);

                var userId = GetUserId();
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
        [Authorize(Roles = Roles.AdministradorSsoma)]
        public async Task<IActionResult> GetPendientes([FromQuery] int? periodoId)
        {
            try
            {
                var periodo = periodoId ?? (await _periodoRepo.GetActivoAsync())?.Id;
                if (periodo == null) return Ok(new EvJefeSsomaCumplimientoDto());
                return Ok(await _repo.GetCumplimientoAsync(periodo.Value));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.GetPendientes"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("resultados")]
        [Authorize(Roles = Roles.AdministradorSsoma)]
        public async Task<IActionResult> GetResultados([FromQuery] int? periodoId)
        {
            try { return Ok(await _repo.GetResultadosAsync(periodoId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvJefeSsomaController.GetResultados"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
