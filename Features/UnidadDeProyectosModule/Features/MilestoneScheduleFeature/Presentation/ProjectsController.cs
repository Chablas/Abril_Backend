using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Constants;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Presentation
{
    [ApiController]
    [Route("api/v1/project")]
    [Authorize]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectsService _service;

        public ProjectsController(IProjectsService service)
        {
            _service = service;
        }

        private int UserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        /// <summary>IDs de rol del JWT (llegan como varios claims ClaimTypes.Role).</summary>
        private int[] RoleIds() => User.FindAll(ClaimTypes.Role)
            .Select(c => int.TryParse(c.Value, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        /// <summary>Tarjetas del Cronograma de Hitos; cada una dice si el usuario es su residente.
        /// El RESIDENTE recibe solo las suyas (ver ProjectsService.GetPagedWithResidents).</summary>
        [HttpGet("paged-with-residents")]
        [RequireFeature(CronogramaHitosFeatures.Ver)]
        public async Task<IActionResult> GetPagedWithResidents(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;

                var result = await _service.GetPagedWithResidents(
                    UserId(), RoleIds(), User.IsInRole(Roles.Residente), page, pageSize, search);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("{projectId:int}/foto")]
        [Consumes("multipart/form-data")]
        [RequireFeature(CronogramaHitosFeatures.Editar, CronogramaHitosFeatures.Administrar)]
        public async Task<IActionResult> UploadFoto(int projectId, IFormFile foto)
        {
            try
            {
                if (foto == null || foto.Length == 0)
                    return BadRequest(new { message = "Debe adjuntar una imagen." });

                var fotoUrl = await _service.UploadFotoAsync(
                    projectId, foto, UserId(), RoleIds(), User.IsInRole(Roles.Residente));
                return Ok(new { message = "Foto actualizada exitosamente.", fotoUrl });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Característica del proyecto de la tarjeta (p. ej. "5 pisos + 2 sótanos").</summary>
        [HttpPatch("{projectId:int}/level-description")]
        [RequireFeature(CronogramaHitosFeatures.Editar, CronogramaHitosFeatures.Administrar)]
        public async Task<IActionResult> UpdateLevelDescription(int projectId, [FromBody] ProjectLevelDescriptionUpdateDTO dto)
        {
            try
            {
                var levelDescription = await _service.UpdateLevelDescriptionAsync(
                    projectId, dto?.LevelDescription, UserId(), RoleIds(), User.IsInRole(Roles.Residente));
                return Ok(new { message = "Característica actualizada exitosamente.", levelDescription });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
