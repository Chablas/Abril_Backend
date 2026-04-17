using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Abril_Backend.Application.Interfaces;

// CRUD de proyectos movido a Features/ConfigurationModule/Features/ProjectFeature/Presentation/ProjectController.cs
// Este controlador conserva únicamente el endpoint que usa el módulo de Proyectos (cronograma de hitos).

namespace Abril_Backend.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectService _projectService;

        public ProjectController(IProjectService projectService)
        {
            _projectService = projectService;
        }

        [Authorize]
        [HttpGet("paged-with-residents")]
        public async Task<IActionResult> GetPagedWithResidents([FromQuery] int page = 1)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                if (page < 1)
                    page = 1;

                var result = await _projectService.GetPagedWithResidents(page);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
