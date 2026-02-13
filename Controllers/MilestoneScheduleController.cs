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
    public class MilestoneScheduleController : ControllerBase
    {
        MilestoneScheduleRepository _repository;
        public MilestoneScheduleController(MilestoneScheduleRepository repository)
        {
            _repository = repository;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int milestoneScheduleId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });
                
                var result = await _repository.GetAllFactory(milestoneScheduleId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}