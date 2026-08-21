using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Application.Interfaces;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Evaluaciones.Presentation.Controllers
{
    // Flujo A: Prevencionista/Coordinador SSOMA evalúan a los supervisores de campo
    // de los contratistas. Solo el Jefe SSOMA (rol 9) puede ver el consolidado.
    [ApiController]
    [Route("api/v1/evaluaciones/supervisores-contratista")]
    [Authorize]
    public class EvSupervisorContratistaController : ControllerBase
    {
        private readonly IEvSupervisorContratistaRepository _repo;
        private readonly IEvPeriodoRepository _periodoRepo;
        private readonly ILogger<EvSupervisorContratistaController> _logger;

        // El Jefe SSOMA (rol 9) también puede evaluar de forma opcional, además de ver
        // el consolidado — a diferencia de Prevencionista/Coordinador (70/72), para quien
        // esta evaluación es su función habitual.
        private const string RolesEvaluador = $"{Roles.CoordinadorSsoma},{Roles.Prevencionista},{Roles.AdministradorSsoma}";

        public EvSupervisorContratistaController(
            IEvSupervisorContratistaRepository repo,
            IEvPeriodoRepository periodoRepo,
            ILogger<EvSupervisorContratistaController> logger)
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
            catch (Exception ex) { _logger.LogError(ex, "Error en EvSupervisorContratistaController.GetInicio"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost]
        [Authorize(Roles = RolesEvaluador)]
        public async Task<IActionResult> Create([FromBody] EvSupervisorContratistaEvaluacionCreateDto dto)
        {
            try
            {
                var periodo = await _periodoRepo.GetActivoAsync()
                    ?? throw new AbrilException("No hay período de evaluación activo.", 400);

                if (dto.Detalles.Count == 0)
                    throw new AbrilException("Debe calificar al menos un criterio.", 400);

                if (dto.Detalles.Any(d => !d.EsNa && (d.Puntaje is null or < 0 or > 4)))
                    throw new AbrilException("El puntaje debe estar entre 0 y 4.", 400);

                var userId = GetUserId();
                var existe = await _repo.ExisteAsync(periodo.Id, dto.SupervisorSsContratistaUsuarioId, userId);
                if (existe)
                    throw new AbrilException("Ya registró una evaluación para este supervisor en este período.", 409);

                var eval = new EvEvaluacionSupervisorContratista
                {
                    PeriodoId = periodo.Id,
                    ProyectoId = dto.ProyectoId,
                    SupervisorWorkerId = dto.SupervisorSsContratistaUsuarioId,
                    EvaluadorUserId = userId,
                    Comentario = dto.Comentario
                };

                var detalles = dto.Detalles.Select(d => new EvEvaluacionSupervisorContratistaDetalle
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
            catch (Exception ex) { _logger.LogError(ex, "Error en EvSupervisorContratistaController.Create"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpPost("no-aplica")]
        [Authorize(Roles = RolesEvaluador)]
        public async Task<IActionResult> MarcarNoAplica([FromBody] EvSupervisorContratistaNoAplicaCreateDto dto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dto.Motivo))
                    throw new AbrilException("Debe indicar el motivo.", 400);

                var periodo = await _periodoRepo.GetActivoAsync()
                    ?? throw new AbrilException("No hay período de evaluación activo.", 400);

                var userId = GetUserId();
                bool esEspecifico = dto.ProyectoId.HasValue && dto.SupervisorSsContratistaUsuarioId.HasValue;

                if (esEspecifico)
                {
                    var existeEspecifico = await _repo.ExisteAsync(periodo.Id, dto.SupervisorSsContratistaUsuarioId!.Value, userId);
                    if (existeEspecifico)
                        throw new AbrilException("Ya registró una evaluación para este supervisor en este período.", 409);
                }
                else
                {
                    var yaMarco = await _repo.ExisteNoAplicaAsync(periodo.Id, userId);
                    if (yaMarco)
                        throw new AbrilException("Ya marcó que no corresponde evaluar supervisores este período.", 409);
                }

                await _repo.RegistrarNoAplicaAsync(periodo.Id, userId, dto.Motivo, dto.ProyectoId, dto.SupervisorSsContratistaUsuarioId);
                return StatusCode(201, new { message = "Registrado correctamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvSupervisorContratistaController.MarcarNoAplica"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("ver")]
        [Authorize(Roles = Roles.AdministradorSsoma)]
        public async Task<IActionResult> GetVer([FromQuery] int? periodoId, [FromQuery] int? proyectoId)
        {
            try { return Ok(await _repo.GetVerInicioAsync(periodoId, proyectoId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvSupervisorContratistaController.GetVer"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = Roles.AdministradorSsoma)]
        public async Task<IActionResult> GetDashboard([FromQuery] int? periodoId, [FromQuery] int? proyectoId)
        {
            try { return Ok(await _repo.GetDashboardAsync(periodoId, proyectoId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex) { _logger.LogError(ex, "Error en EvSupervisorContratistaController.GetDashboard"); return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
