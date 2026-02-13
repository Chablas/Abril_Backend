using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Infrastructure.Repositories;
using Abril_Backend.Application.DTOs;
using Abril_Backend.Application.Exceptions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Abril_Backend.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class UserProjectController : ControllerBase
    {
        UserProjectRepository _userProjectRepository;
        UserRepository _userRepository;
        ProjectRepository _projectRepository;
        public UserProjectController(UserProjectRepository userProjectRepository, UserRepository userRepository, ProjectRepository projectRepository)
        {
            _userProjectRepository = userProjectRepository;
            _userRepository = userRepository;
            _projectRepository = projectRepository;
        }

        [Authorize]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPagedFactory([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                if (page < 1)
                    page = 1;

                var result = await _userProjectRepository.GetPagedFactory(page, pageSize);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpGet("users-without-lessons-this-month")]
        public async Task<IActionResult> GetUsersWithoutLessonsThisMonth()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _userProjectRepository.GetUsersWithoutLessonsThisMonth();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UserProjectCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                var userProjectId = await _userProjectRepository.Create(dto, userId);

                return Ok(new { message = "Proyecto asignado exitosamente" });
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
        [HttpGet("data-create")]
        public async Task<IActionResult> GetDataCreate()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var usersTask = _userRepository.GetAllFilterFactory();
                var projectsTask = _projectRepository.GetAllFilterFactory();

                await Task.WhenAll(usersTask, projectsTask);

                var response = new UserProjectCreateDataDTO
                {
                    UserPersons = await usersTask,
                    Projects = await projectsTask
                };

                return Ok(response);
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
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                var result = await _userProjectRepository.DeleteSoftAsync(id, userId);

                return Ok(new { message = "El registro ha sido eliminado." });
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