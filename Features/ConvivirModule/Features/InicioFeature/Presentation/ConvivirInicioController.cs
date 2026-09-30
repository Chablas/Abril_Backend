using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConvivirModule.Features.InicioFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.ConvivirModule.Features.InicioFeature.Presentation
{
    /// <summary>
    /// Inicio de la app. Solo el rol PROPIETARIO, y cada uno ve solo sus propiedades: el
    /// repositorio filtra por el usuario del token, no por lo que mande la app.
    /// </summary>
    [ApiController]
    [Authorize(Roles = Roles.Propietario)]
    [Route("api/v1/convivir/inicio")]
    public class ConvivirInicioController : ControllerBase
    {
        private readonly IConvivirInicioService _service;

        public ConvivirInicioController(IConvivirInicioService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetInicio([FromQuery] int? propietarioId = null)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                    return Unauthorized(new { message = "Tu sesión venció. Vuelve a ingresar." });

                return Ok(await _service.GetInicio(userId, propietarioId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "No pudimos cargar tu información. Inténtalo de nuevo en unos minutos." });
            }
        }
    }
}
