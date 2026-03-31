using Microsoft.AspNetCore.Mvc;
using Abril_Backend.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Abril_Backend.Features.Adjudicaciones.Application.Interfaces;
using Abril_Backend.Features.Adjudicaciones.Application.Dtos;

namespace Abril_Backend.Features.Adjudicaciones.Presentation
{

    [ApiController]
    [Route("api/v1/[controller]")]
    public class ProjectSubContractorController : ControllerBase
    {
        IProjectSubContractorService _projectSubContractorService;
        public ProjectSubContractorController(IProjectSubContractorService projectSubContractorService)
        {
            _projectSubContractorService = projectSubContractorService;
        }

        /*[Authorize]
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });
                if (page < 1)
                    page = 1;
                var result = await _projectSubContractorService.GetPaged(page);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }*/

        [Authorize]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] ProjectSubContractorCreateDTO dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                await _projectSubContractorService.Create(dto, userId);
                return Ok(new { message = "Adjudicación creada exitosamente" });
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

        //[Authorize]
        [HttpGet("form-data")]
        public async Task<IActionResult> GetFormData()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);

                var data = await _projectSubContractorService.GetFormData();

                return Ok(data);
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