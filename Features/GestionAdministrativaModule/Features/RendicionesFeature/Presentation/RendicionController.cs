using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.Rendiciones.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionAdministrativa.Rendiciones.Presentation
{
    /// <summary>
    /// "Mis Rendiciones": las planillas propias, su primera revisión y el seguimiento de su
    /// reembolso. El trabajador solo envía la planilla a revisión y la subsana si vuelve observada:
    /// el Consolidado del S10, el aviso a la jefatura y la corrección con el ERP son del
    /// consolidador. Todos los endpoints están acotados al trabajador del usuario autenticado — no
    /// hay forma de pedir la planilla de otro.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/rendiciones")]
    [Authorize]
    public class RendicionController : ControllerBase
    {
        private readonly IRendicionService _service;
        private readonly ILogger<RendicionController> _logger;

        public RendicionController(IRendicionService service, ILogger<RendicionController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        private int? CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;

        [HttpGet]
        public async Task<IActionResult> GetMisRendiciones(
            [FromQuery] string? estadoPrimeraRevision,
            [FromQuery] string? estadoReembolso,
            [FromQuery] bool? conConsolidado,
            [FromQuery] int? periodoAnio = null,
            [FromQuery] int? periodoMes = null)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });

                var filters = new RendicionFiltersDto
                {
                    EstadoPrimeraRevision = estadoPrimeraRevision,
                    EstadoReembolso = estadoReembolso,
                    ConConsolidado  = conConsolidado,
                    PeriodoAnio     = periodoAnio,
                    PeriodoMes      = periodoMes,
                };
                return Ok(await _service.GetByUserId(userId.Value, filters));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RendicionController.GetMisRendiciones");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("filter-data")]
        public async Task<IActionResult> GetFilterData()
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.GetFilterData(userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RendicionController.GetFilterData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("{id:int}/detalle")]
        public async Task<IActionResult> GetDetalle(int id)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(await _service.GetDetalle(id, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RendicionController.GetDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Envía la planilla a la primera revisión de la jefatura (el "Enviar" de RG-30). Manda los
        /// dos correos del paso: la confirmación al solicitante y el aviso al jefe con los botones
        /// de aprobar y observar.
        /// </summary>
        [HttpPatch("{id:int}/enviar-revision")]
        public async Task<IActionResult> EnviarAPrimeraRevision(int id)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });
                return Ok(new { message = await _service.EnviarAPrimeraRevision(id, userId.Value) });
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RendicionController.EnviarAPrimeraRevision");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Vuelve a generar el PDF de una planilla observada en primera revisión y lo descarga. La
        /// rendición conserva su código REN-AAAA-NNNN y su número de planilla, y queda lista para
        /// reenviar a revisión.
        /// </summary>
        [HttpPatch("{id:int}/regenerar-planilla")]
        public async Task<IActionResult> RegenerarPlanilla(int id)
        {
            try
            {
                var userId = CurrentUserId;
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });

                var pdf = await _service.RegenerarPlanilla(id, userId.Value);

                Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
                return File(pdf, "application/pdf", $"Planilla_Rendicion_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RendicionController.RegenerarPlanilla");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
