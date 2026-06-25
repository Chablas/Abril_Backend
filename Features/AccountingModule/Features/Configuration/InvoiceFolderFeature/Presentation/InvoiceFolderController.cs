using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Application.Interfaces;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.InvoiceFolderFeature.Presentation
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class InvoiceFolderController : ControllerBase
    {
        private readonly IInvoiceFolderService _service;

        public InvoiceFolderController(IInvoiceFolderService service)
        {
            _service = service;
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged([FromQuery] int page = 1)
        {
            try
            {
                if (GetUserId() == null) return Unauthorized(new { message = "Inicie sesión" });

                var result = await _service.GetPaged(new InvoiceFolderFilterDto { Page = page });
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost("resolve-link")]
        public async Task<IActionResult> ResolveLink([FromBody] ResolveLinkRequestDto dto)
        {
            try
            {
                if (GetUserId() == null) return Unauthorized(new { message = "Inicie sesión" });

                var result = await _service.ResolveLink(dto.LinkUrl);
                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("folders")]
        public async Task<IActionResult> GetFolders([FromQuery] string driveId, [FromQuery] string folderId)
        {
            try
            {
                if (GetUserId() == null) return Unauthorized(new { message = "Inicie sesión" });

                var result = await _service.GetChildFolders(driveId, folderId);
                return Ok(result);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] InvoiceFolderCreateDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión" });

                await _service.Create(dto, userId.Value);
                return Ok(new { message = "Carpeta registrada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] InvoiceFolderUpdateDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión" });

                await _service.Update(dto, userId.Value);
                return Ok(new { message = "Carpeta actualizada exitosamente." });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpDelete("{invoiceFolderId}")]
        public async Task<IActionResult> Delete(int invoiceFolderId)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión" });

                var result = await _service.Delete(invoiceFolderId, userId.Value);
                if (!result)
                    return NotFound(new { message = "La carpeta no existe." });

                return Ok(new { message = "Carpeta eliminada exitosamente." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : null;
        }
    }
}
