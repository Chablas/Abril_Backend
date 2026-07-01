using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Evaluaciones.Presentation.Controllers
{
    [ApiController]
    [Route("api/v1/evaluaciones/contratistas")]
    [Authorize]
    public class EvContratistaController : ControllerBase
    {
        private readonly IEvContratistaRepository _repo;
        private readonly IEvPeriodoRepository _periodoRepo;
        private readonly ILogger<EvContratistaController> _logger;

        public EvContratistaController(
            IEvContratistaRepository repo,
            IEvPeriodoRepository periodoRepo,
            ILogger<EvContratistaController> logger)
        {
            _repo = repo;
            _periodoRepo = periodoRepo;
            _logger = logger;
        }

        private int GetUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        /// <summary>Datos iniciales para la pantalla Evaluar Contratista.</summary>
        [HttpGet("inicio")]
        public async Task<IActionResult> GetInicio()
        {
            try
            {
                return Ok(await _repo.GetInicioAsync(GetUserId()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en EvContratistaController.GetInicio");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Datos para la lista Ver Evaluaciones (con filtros opcionales).</summary>
        [HttpGet("ver")]
        public async Task<IActionResult> GetVer([FromQuery] int? periodoId, [FromQuery] int? proyectoId)
        {
            try
            {
                return Ok(await _repo.GetVerInicioAsync(periodoId, proyectoId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en EvContratistaController.GetVer");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Dashboard ejecutivo.</summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard([FromQuery] int? periodoId, [FromQuery] int? proyectoId)
        {
            try
            {
                return Ok(await _repo.GetDashboardAsync(periodoId, proyectoId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en EvContratistaController.GetDashboard");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Registrar evaluación de un contratista.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EvContratistaEvaluacionCreateDto dto)
        {
            try
            {
                var periodo = await _periodoRepo.GetActivoAsync()
                    ?? throw new AbrilException("No hay período de evaluación activo.", 400);

                if (string.IsNullOrWhiteSpace(dto.Comentario) == false && dto.Detalles.Count == 0)
                    throw new AbrilException("Debe calificar al menos un criterio.", 400);

                if (dto.Detalles.Any(d => !d.EsNa && (d.Puntaje is null or < 0 or > 4)))
                    throw new AbrilException("El puntaje debe estar entre 0 y 4.", 400);

                // Determinar el área del evaluador en el repositorio
                var inicio = await _repo.GetInicioAsync(GetUserId());
                var areaNombre = inicio.MiAreaNombre
                    ?? throw new AbrilException("No se pudo determinar su área evaluadora. Verifique su perfil de trabajador.", 400);

                var existe = await _repo.ExisteAsync(periodo.Id, dto.ProyectoId, dto.ContributorId, areaNombre, GetUserId());
                if (existe)
                    throw new AbrilException("Ya registró una evaluación para este contratista en este período.", 409);

                var eval = new EvEvaluacionContratista
                {
                    PeriodoId = periodo.Id,
                    ProyectoId = dto.ProyectoId,
                    ContributorId = dto.ContributorId,
                    AreaNombre = areaNombre,
                    EvaluadorUserId = GetUserId(),
                    Comentario = dto.Comentario
                };

                var detalles = dto.Detalles.Select(d => new EvEvaluacionContratistaDetalle
                {
                    PlantillaId = d.PlantillaId,
                    Criterio = d.Criterio,
                    Puntaje = d.EsNa ? null : d.Puntaje,
                    EsNa = d.EsNa
                }).ToList();

                var result = await _repo.CreateAsync(eval, detalles);
                return StatusCode(201, new { result.Id, result.Nota, message = "Evaluación registrada correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en EvContratistaController.Create");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
