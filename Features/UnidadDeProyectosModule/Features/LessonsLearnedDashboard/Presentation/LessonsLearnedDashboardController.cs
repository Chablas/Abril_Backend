using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Presentation
{
    [ApiController]
    [Route("api/v1/lessons-dashboard")]
    public class LessonsLearnedDashboardController : ControllerBase
    {
        private readonly ILessonsLearnedDashboardService _lessonsLearnedDashboardService;

        public LessonsLearnedDashboardController(ILessonsLearnedDashboardService lessonsLearnedDashboardService)
        {
            _lessonsLearnedDashboardService = lessonsLearnedDashboardService;
        }

        [Authorize]
        [HttpGet("filters")]
        public async Task<IActionResult> GetFilters()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _lessonsLearnedDashboardService.GetFilters();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
