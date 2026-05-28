using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Abril_Backend.Application.Interfaces;
using Abril_Backend.Application.Exceptions;

// CRUD de proyectos movido a Features/ConfigurationModule/Features/ProjectFeature/Presentation/ProjectController.cs
// Este controlador conserva únicamente el endpoint que usa el módulo de Proyectos (cronograma de hitos).

namespace Abril_Backend.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectService _projectService;

        public ProjectController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        [Authorize]
        [HttpPatch("{projectId:int}/foto")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFoto(int projectId, IFormFile foto)
        {
            try
            {
                if (foto == null || foto.Length == 0)
                    return BadRequest(new { message = "Debe adjuntar una imagen." });

                var fotoUrl = await _projectService.UploadFotoAsync(projectId, foto);
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

        [Authorize]
        [HttpGet("paged-with-residents")]
        public async Task<IActionResult> GetPagedWithResidents([FromQuery] int page = 1, [FromQuery] string? search = null)
        {
            try
            {
                if (page < 1)
                    page = 1;

                var result = await _projectService.GetPagedWithResidents(page, search);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
