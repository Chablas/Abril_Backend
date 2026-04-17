using Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos;
using Abril_Backend.Features.Contractors.ContractorManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.Contractors.ContractorManagement.Presentation
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class ContractorManagementController : ControllerBase
    {
        private readonly IContractorManagementService _service;

        public ContractorManagementController(IContractorManagementService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetPaged([FromQuery] CompanyFilterDto filter)
        {
            try
            {
                var result = await _service.GetPaged(filter);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("{contractorId}/approve")]
        public async Task<IActionResult> Approve(int contractorId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _service.Approve(contractorId, userId);
                return Ok(new { message = "Contratista aprobado exitosamente." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPatch("{contractorId}/reject")]
        public async Task<IActionResult> Reject(int contractorId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _service.Reject(contractorId, userId);
                return Ok(new { message = "Contratista rechazado exitosamente." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
