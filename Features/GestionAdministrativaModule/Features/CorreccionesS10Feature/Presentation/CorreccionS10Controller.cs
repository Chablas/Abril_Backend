using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionAdministrativa.CorreccionesS10.Presentation
{
    /// <summary>
    /// "Correcciones S10": la bandeja del Coordinador ERP. Basta con el rol COORDINADOR ERP del
    /// token, y es el ÚNICO que entra — no hay ningún otro camino a estos endpoints.
    ///
    /// El pedido de corrección NO se crea acá: lo dispara el colaborador desde Mis Rendiciones
    /// (ver <c>RendicionController.SolicitarCorreccionS10</c>), que es la pantalla donde se origina
    /// y donde viven los guards de propiedad de la planilla.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/correcciones-s10")]
    [Authorize(Roles = Roles.CoordinadorErp)]
    public class CorreccionS10Controller : ControllerBase
    {
        private readonly ICorreccionS10Service _service;
        private readonly ILogger<CorreccionS10Controller> _logger;

        public CorreccionS10Controller(
            ICorreccionS10Service service, ILogger<CorreccionS10Controller> logger)
        {
            _service = service;
            _logger  = logger;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? estado,
            [FromQuery] int? workerId,
            [FromQuery] string? q,
            [FromQuery] int? periodoAnio = null,
            [FromQuery] int? periodoMes = null)
        {
            try
            {
                return Ok(await _service.GetAll(new CorreccionS10FiltersDto
                {
                    Estado      = estado,
                    WorkerId    = workerId,
                    Q           = q,
                    PeriodoAnio = periodoAnio,
                    PeriodoMes  = periodoMes,
                }));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreccionS10Controller.GetAll");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("filter-data")]
        public async Task<IActionResult> GetFilterData()
        {
            try
            {
                return Ok(await _service.GetFilterData());
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreccionS10Controller.GetFilterData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("{id:int}/detalle")]
        public async Task<IActionResult> GetDetalle(int id)
        {
            try
            {
                return Ok(await _service.GetDetalle(id));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreccionS10Controller.GetDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// A quién le llegaría el aviso al colaborador si se confirmara la selección. Es POST y no
        /// GET porque la selección puede ser larga y viaja en el cuerpo, igual que en las otras
        /// pantallas del flujo.
        /// </summary>
        [HttpPost("correo-preview")]
        public async Task<IActionResult> GetCorreoPreview([FromBody] List<int> correccionIds)
        {
            try
            {
                return Ok(await _service.GetCorreoPreview(correccionIds ?? new List<int>()));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreccionS10Controller.GetCorreoPreview");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// El check de confirmación del Coordinador ERP (RG-22 / RF-OBS-07): la corrección ya se
        /// hizo en el S10. Sirve para una fila o para varias — la bandeja usa el mismo endpoint
        /// desde el detalle y desde la tabla.
        /// </summary>
        [HttpPatch("atender")]
        public async Task<IActionResult> Atender([FromBody] AtenderCorreccionS10BulkDto dto)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.Atender(dto, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreccionS10Controller.Atender");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
