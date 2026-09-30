using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConvivirModule.Features.MiProyectoFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.ConvivirModule.Features.MiProyectoFeature.Presentation
{
    /// <summary>Mi Proyecto de la app. Mismo acceso que Inicio: solo el rol PROPIETARIO y sus propiedades.</summary>
    [ApiController]
    [Authorize(Roles = Roles.Propietario)]
    [Route("api/v1/convivir/mi-proyecto")]
    public class ConvivirMiProyectoController : ControllerBase
    {
        private readonly IConvivirMiProyectoService _service;

        public ConvivirMiProyectoController(IConvivirMiProyectoService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetMiProyecto([FromQuery] int? propietarioId = null)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                    return Unauthorized(new { message = "Tu sesión venció. Vuelve a ingresar." });

                return Ok(await _service.GetMiProyecto(userId, propietarioId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "No pudimos cargar el avance de tu proyecto. Inténtalo de nuevo en unos minutos." });
            }
        }
    }
}
