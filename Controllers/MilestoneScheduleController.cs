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

        //[Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllByMilestoneScheduleHistoryIdFactory([FromQuery] int milestoneScheduleHistoryId)
        {
            try
            {
                /*var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });*/

                var result = await _repository.GetAllByMilestoneScheduleHistoryIdFactory(milestoneScheduleHistoryId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
        /*
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MilestoneScheduleCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                if (string.IsNullOrWhiteSpace(dto.MilestoneScheduleDescription))
                    return BadRequest(new { message = "MilestoneScheduleDescription es obligatorio." });
                var result = await _repository.Create(dto, userId);
                return Ok(new { message = "MilestoneSchedule creada exitosamente" });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
        */
    }
}