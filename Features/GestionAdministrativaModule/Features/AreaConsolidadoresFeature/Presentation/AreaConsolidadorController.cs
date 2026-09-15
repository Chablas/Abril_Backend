using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.AreaConsolidadores.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionAdministrativa.AreaConsolidadores.Presentation
{
    /// <summary>
    /// Consolidadores del S10 por área (Gestión de Rendiciones → Configuración → Consolidadores).
    /// Mismo contrato que Revisores de Áreas: lo que cambia es a quién se asigna y que acá quedan
    /// vigentes todos los activos, no solo el primero.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/configuracion/consolidadores-areas")]
    [Authorize]
    public class AreaConsolidadorController : ControllerBase
    {
        /// <summary>
        /// Roles que configuran esta pantalla. Es el mismo par que en Revisores de Áreas: quien
        /// administra la jefatura de un área administra también quién puede consolidar por ella.
        /// </summary>
        private const string RolesQueEditan =
            Roles.AdministradorSolicitudSalidas + "," + Roles.UsuarioGth;

        private readonly IAreaConsolidadorService _service;
        private readonly ILogger<AreaConsolidadorController> _logger;

        public AreaConsolidadorController(
            IAreaConsolidadorService service, ILogger<AreaConsolidadorController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>
        /// Carga inicial: gerencias y áreas estándar con sus n consolidadores + opciones del
        /// selector. ADMINISTRADOR DE SOLICITUD DE SALIDAS y USUARIO DE GTH ven todas las áreas y
        /// pueden editarlas; un Jefe/Coordinador/Gerente ve solo la suya, de lectura.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetInitialData()
        {
            try
            {
                var userId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
                    ? id : (int?)null;
                if (userId == null)
                    return Unauthorized(new { message = "Usuario no autenticado." });

                var verTodas = User.IsInRole(Roles.AdministradorSolicitudSalidas)
                               || User.IsInRole(Roles.UsuarioGth);
                return Ok(await _service.GetInitialDataAsync(userId.Value, verTodas));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AreaConsolidadorController.GetInitialData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Reemplaza el conjunto de consolidadores de un área o de un proyecto dentro del área
        /// (dto.ProjectId con valor).
        /// </summary>
        [HttpPut("{areaScopeId:int}")]
        [Authorize(Roles = RolesQueEditan)]
        public async Task<IActionResult> UpdateConsolidadores(
            int areaScopeId, [FromBody] AreaAsignacionUpdateDto dto)
        {
            try
            {
                await _service.UpdateAreaConsolidadoresAsync(
                    areaScopeId, dto?.ProjectId, dto?.Asignados ?? new List<AreaAsignacionInputDto>());
                return Ok(new { message = "Consolidadores del área actualizados exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AreaConsolidadorController.UpdateConsolidadores");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Marca/desmarca "filtrar por proyecto" para un área. La bandera es del área y la comparte
        /// con Revisores de Áreas: cambiarla acá la cambia allá.
        /// </summary>
        [HttpPut("{areaScopeId:int}/filtro-proyecto")]
        [Authorize(Roles = RolesQueEditan)]
        public async Task<IActionResult> SetFiltroProyecto(
            int areaScopeId, [FromBody] AreaFiltroProyectoUpdateDto dto)
        {
            try
            {
                await _service.SetFiltroProyectoAsync(areaScopeId, dto?.FiltraPorProyecto ?? false);
                return Ok(new { message = "Configuración del área actualizada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AreaConsolidadorController.SetFiltroProyecto");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
