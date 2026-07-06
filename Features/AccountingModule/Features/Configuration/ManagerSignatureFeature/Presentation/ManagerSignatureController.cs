using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Dtos;
using Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Application.Interfaces;

namespace Abril_Backend.Features.AccountingModule.Features.Configuration.ManagerSignatureFeature.Presentation
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class ManagerSignatureController : ControllerBase
    {
        private readonly IManagerSignatureService _service;

        public ManagerSignatureController(IManagerSignatureService service)
        {
            _service = service;
        }

        /// <summary>Firma del usuario actual (null si aún no la configuró).</summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión" });

                var result = await _service.Get(userId.Value);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Guarda/actualiza la firma del usuario actual (PNG dibujado en el canvas).</summary>
        [HttpPut]
        public async Task<IActionResult> Save([FromBody] ManagerSignatureSaveDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión" });

                var result = await _service.Save(dto, userId.Value);
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

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : null;
        }
    }
}
