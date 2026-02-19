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
    public class MilestoneScheduleHistoryController : ControllerBase
    {
        MilestoneScheduleHistoryRepository _repository;
        public MilestoneScheduleHistoryController(MilestoneScheduleHistoryRepository repository)
        {
            _repository = repository;
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllByScheduleIdFactory([FromQuery] int scheduleId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _repository.GetAllByScheduleIdFactory(scheduleId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MilestoneScheduleHistoryCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                var result = await _repository.Create(dto, userId);
                return Ok(new { message = "Cronograma creado exitosamente" });
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

        /*
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MilestoneScheduleHistoryCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                if (string.IsNullOrWhiteSpace(dto.MilestoneScheduleHistoryDescription))
                    return BadRequest(new { message = "MilestoneScheduleHistoryDescription es obligatorio." });
                var result = await _repository.Create(dto, userId);
                return Ok(new { message = "MilestoneScheduleHistory creada exitosamente" });
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
        [Authorize]
        [HttpPut]
        public async Task<IActionResult> Edit([FromBody] MilestoneScheduleHistoryEditDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                if (string.IsNullOrWhiteSpace(dto.MilestoneScheduleHistoryDescription))
                    return BadRequest(new { message = "MilestoneScheduleHistoryDescription es obligatorio." });
                var result = await _repository.Update(dto, userId);
                return Ok(new { message = "MilestoneScheduleHistory actualizada exitosamente." });
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