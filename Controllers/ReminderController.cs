using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Abril_Backend.Application.Interfaces;

namespace Abril_Backend.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ReminderController : ControllerBase
    {
        private readonly IReminderService _service;
        public ReminderController(IReminderService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> ExecuteReminders()
        {
            try
            {
                /*var authHeader = Request.Headers["Authorization"].FirstOrDefault();

                if (authHeader != $"Bearer {Environment.GetEnvironmentVariable("CronSecret")}")
                {
                    return Unauthorized();
                }*/

                await _service.ExecuteReminders();
                return NoContent();
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
    }
}