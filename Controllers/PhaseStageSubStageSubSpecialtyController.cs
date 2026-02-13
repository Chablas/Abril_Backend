using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Abril_Backend.Controllers
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class PhaseStageSubStageSubSpecialtyController : ControllerBase
    {
        PhaseStageSubStageSubSpecialtyRepository _phaseStageSubStageSubSpecialtyRepository;
        AreaRepository _areaRepository;
        ProjectRepository _projectRepository;
        PhaseRepository _phaseRepository;
        StageRepository _stageRepository;
        LayerRepository _layerRepository;
        SubStageRepository _subStageRepository;
        SubSpecialtyRepository _subSpecialtyRepository;

        public PhaseStageSubStageSubSpecialtyController(
            PhaseStageSubStageSubSpecialtyRepository phaseStageSubStageSubSpecialtyRepository,
            AreaRepository areaRepository,
            ProjectRepository projectRepository,
            PhaseRepository phaseRepository,
            StageRepository stageRepository,
            LayerRepository layerRepository,
            SubStageRepository subStageRepository,
            SubSpecialtyRepository subSpecialtyRepository
        )
        {
            _phaseStageSubStageSubSpecialtyRepository = phaseStageSubStageSubSpecialtyRepository;
            _areaRepository = areaRepository;
            _projectRepository = projectRepository;
            _phaseRepository = phaseRepository;
            _stageRepository = stageRepository;
            _layerRepository = layerRepository;
            _subStageRepository = subStageRepository;
            _subSpecialtyRepository = subSpecialtyRepository;
        }

        [Authorize]
        [HttpGet("form-data")]
        public async Task<IActionResult> GetFormData()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var areasTask = _areaRepository.GetAllFactory();
                var projectsTask = _projectRepository.GetAllFactory();
                var phasesTask = _phaseRepository.GetAllFactory();
                var stagesTask = _stageRepository.GetAllFactory();
                var layersTask = _layerRepository.GetAllFactory();
                var subStagesTask = _subStageRepository.GetAllFactory();
                var subSpecialtiesTask = _subSpecialtyRepository.GetAllFactory();

                await Task.WhenAll(areasTask, projectsTask, phasesTask, stagesTask, layersTask, subStagesTask, subSpecialtiesTask);

                var result = new
                {
                    Areas = areasTask.Result,
                    Projects = projectsTask.Result,
                    Phases = phasesTask.Result,
                    Stages = stagesTask.Result,
                    Layers = layersTask.Result,
                    SubStages = subStagesTask.Result,
                    SubSpecialties = subSpecialtiesTask.Result
                };

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PhaseStageSubStageSubSpecialtyCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                var result = await _phaseStageSubStageSubSpecialtyRepository.Create(dto, userId);
                if (result == null)
                    return BadRequest(new { message = "La relación ya existe."} );
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                var result = await _phaseStageSubStageSubSpecialtyRepository.DeleteSoftAsync(id, userId);
                if (!result)
                    return NotFound(new { message = "Relación no encontrada."});
                return NoContent();
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                return Ok(await _phaseStageSubStageSubSpecialtyRepository.GetPaged(page));
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}