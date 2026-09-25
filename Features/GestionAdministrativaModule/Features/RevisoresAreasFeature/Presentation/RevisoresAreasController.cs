using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionAdministrativa.RevisoresAreas.Presentation
{
    /// <summary>
    /// Gestión Administrativa → Configuración → Revisores de Áreas. Reemplaza a las tres secciones
    /// que hacían lo mismo por separado (Revisores de Áreas de Solicitud de Salidas y de Mis
    /// Rendiciones, y Consolidadores de Consolidados).
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/configuracion/revisores-areas")]
    [Authorize]
    public class RevisoresAreasController : ControllerBase
    {
        /// <summary>
        /// Roles que editan, en el formato separado por comas que espera
        /// <c>[Authorize(Roles = ...)]</c>. Son también los que ven todas las áreas.
        /// </summary>
        private const string RolesQueEditan =
            Roles.AdministradorSolicitudSalidas + "," + Roles.UsuarioGth;

        private readonly IRevisoresAreasService _service;
        private readonly ILogger<RevisoresAreasController> _logger;

        public RevisoresAreasController(IRevisoresAreasService service, ILogger<RevisoresAreasController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>
        /// Carga inicial: las áreas configurables con los cinco actores de un trabajador normal de
        /// cada una (y de cada obra en las que se parten por obra), los catálogos y el selector de
        /// personas. Una jefatura sin esos roles ve solo su área, sin editar.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetInitialData()
        {
            try
            {
                var userId = UsuarioId();
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });

                return Ok(await _service.GetInitialDataAsync(userId.Value, VeTodas()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RevisoresAreasController.GetInitialData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// El detalle de una fila: los cinco actores de cada tipo de trabajador que aplica a ella, y
        /// lo personalizado en la fila para editarlo.
        /// </summary>
        [HttpGet("{areaScopeId:int}")]
        public async Task<IActionResult> GetDetalle(int areaScopeId, [FromQuery] int? projectId)
        {
            try
            {
                var userId = UsuarioId();
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });

                return Ok(await _service.GetDetalleAsync(userId.Value, VeTodas(), areaScopeId, projectId));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RevisoresAreasController.GetDetalle");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Guarda de una vez todas las celdas de una fila y devuelve su detalle recalculado.</summary>
        [HttpPut("{areaScopeId:int}")]
        [Authorize(Roles = RolesQueEditan)]
        public async Task<IActionResult> Guardar(int areaScopeId, [FromBody] RevisoresAreaGuardarDto dto)
        {
            try
            {
                var userId = UsuarioId();
                if (userId == null) return Unauthorized(new { message = "Usuario no autenticado." });

                return Ok(await _service.GuardarAsync(userId.Value, areaScopeId, dto ?? new RevisoresAreaGuardarDto()));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RevisoresAreasController.Guardar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        private int? UsuarioId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        /// <summary>Ver todas las áreas y editarlas van juntos: los dos roles que administran la pantalla.</summary>
        private bool VeTodas() =>
            User.IsInRole(Roles.AdministradorSolicitudSalidas) || User.IsInRole(Roles.UsuarioGth);
    }
}
