using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Constants;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Dtos;
using Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.PropietariosModule.Features.GestionPropietariosFeature.Presentation
{
    /// <summary>
    /// Pantalla Propietarios de la intranet. Todo pide la feature de la pantalla: da de alta
    /// cuentas y expone datos personales, no basta con estar autenticado.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequireFeature(PropietariosFeatures.Gestion)]
    [Route("api/v1/propietarios")]
    public class GestionPropietariosController : ControllerBase
    {
        private const string ErrorServidor = "Error del servidor. Por favor contactar al administrador del sistema.";

        private readonly IGestionPropietariosService _service;

        public GestionPropietariosController(IGestionPropietariosService service)
        {
            _service = service;
        }

        private int? UsuarioActual() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        /// <summary>Carga inicial: proyectos (filtro y formulario) + primera página.</summary>
        [HttpGet("init")]
        public async Task<IActionResult> GetInit([FromQuery] int pageSize = 10)
        {
            try
            {
                return Ok(await _service.GetInit(pageSize));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        /// <summary>Búsqueda, filtro de proyecto o cambio de página: solo la tabla.</summary>
        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] int? projectId = null)
        {
            try
            {
                return Ok(await _service.GetPaged(page, pageSize, search, projectId));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        /// <summary>Lupa del DNI en el modal de crear: la persona del sistema o los datos de RENIEC.</summary>
        [HttpGet("persona")]
        public async Task<IActionResult> BuscarPersona([FromQuery] string dni)
        {
            try
            {
                return Ok(await _service.BuscarPersona(dni));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] PropietarioCreateDto dto)
        {
            try
            {
                var userId = UsuarioActual();
                if (userId == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                return Ok(await _service.Crear(dto, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        [HttpPut("{personId:int}")]
        public async Task<IActionResult> Actualizar(int personId, [FromBody] PropietarioUpdateDto dto)
        {
            try
            {
                var userId = UsuarioActual();
                if (userId == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                return Ok(await _service.Actualizar(personId, dto, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        [HttpPost("{personId:int}/reenviar-invitacion")]
        public async Task<IActionResult> ReenviarInvitacion(int personId)
        {
            try
            {
                var email = await _service.ReenviarInvitacion(personId);
                return Ok(new { email });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }

        [HttpDelete("{personId:int}")]
        public async Task<IActionResult> Eliminar(int personId)
        {
            try
            {
                var userId = UsuarioActual();
                if (userId == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                await _service.Eliminar(personId, userId.Value);
                return Ok();
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = ErrorServidor });
            }
        }
    }
}
