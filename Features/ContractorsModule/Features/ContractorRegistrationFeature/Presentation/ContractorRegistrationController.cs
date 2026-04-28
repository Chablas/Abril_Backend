using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorRegistration.Application.Interfaces;
using Abril_Backend.Shared.Services.Reniec.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Abril_Backend.Features.Contractors.ContractorRegistration.Presentation
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ContractorRegistrationController : ControllerBase
    {
        private readonly IContractorRegistrationService _service;
        private readonly IReniecService _reniecService;

        public ContractorRegistrationController(IContractorRegistrationService service, IReniecService reniecService)
        {
            _service = service;
            _reniecService = reniecService;
        }

        [HttpGet("dni/{dni}")]
        public async Task<IActionResult> GetByDni(string dni)
        {
            try
            {
                var result = await _reniecService.GetByDniAsync(dni);
                if (result is null)
                    return NotFound(new { message = "No se encontró información para el DNI proporcionado." });

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("ruc/{ruc}")]
        //[EnableRateLimiting("sunat-ruc")]
        public async Task<IActionResult> GetByRuc(string ruc)
        {
            try
            {
                var result = await _service.GetByRuc(ruc);
                if (result is null)
                    return NotFound(new { message = "No se encontró información para el RUC proporcionado." });

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] ContributorCreateDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                int? userId = userIdClaim != null ? int.Parse(userIdClaim.Value) : null;

                await _service.Create(dto, userId, dto.GraphAccessToken);
                return Ok(new { message = "Contratista registrado exitosamente." });
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
