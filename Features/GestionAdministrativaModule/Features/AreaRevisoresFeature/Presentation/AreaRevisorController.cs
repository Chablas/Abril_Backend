using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Presentation
{
    [ApiController]
    [Route("api/v1/gestion-administrativa/configuracion/revisores-areas")]
    [Authorize]
    public class AreaRevisorController : ControllerBase
    {
        private readonly IAreaRevisorService _service;
        private readonly ILogger<AreaRevisorController> _logger;

        public AreaRevisorController(IAreaRevisorService service, ILogger<AreaRevisorController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>
        /// Carga inicial: áreas estándar (primer nodo de cada rama) con sus n revisores + opciones.
        /// ADMINISTRADOR DE SOLICITUD DE SALIDAS ve todas las áreas; un trabajador con
        /// categoría Jefe/Coordinador/Gerente ve solo su área; el resto no ve ninguna.
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

                var verTodas = User.IsInRole(Roles.AdministradorSolicitudSalidas);
                return Ok(await _service.GetInitialDataAsync(userId.Value, verTodas));
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AreaRevisorController.GetInitialData");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Reemplaza el conjunto de revisores de un área (nodo area_scope) o de un proyecto
        /// dentro del área (dto.ProjectId con valor). Solo el ADMINISTRADOR DE SOLICITUD DE SALIDAS puede editar.
        /// </summary>
        [HttpPut("{areaScopeId:int}")]
        [Authorize(Roles = Roles.AdministradorSolicitudSalidas)]
        public async Task<IActionResult> UpdateRevisores(int areaScopeId, [FromBody] AreaRevisoresUpdateDto dto)
        {
            try
            {
                await _service.UpdateAreaRevisoresAsync(areaScopeId, dto?.ProjectId, dto?.Revisores ?? new List<AreaRevisorAsignacionDto>());
                return Ok(new { message = "Revisores del área actualizados exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AreaRevisorController.UpdateRevisores");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Marca/desmarca "filtrar por proyecto" para un área. Al activarse, el área se
        /// subdivide por proyecto y sus revisores se asignan por proyecto.
        /// Solo el ADMINISTRADOR DE SOLICITUD DE SALIDAS puede editar.
        /// </summary>
        [HttpPut("{areaScopeId:int}/filtro-proyecto")]
        [Authorize(Roles = Roles.AdministradorSolicitudSalidas)]
        public async Task<IActionResult> SetFiltroProyecto(int areaScopeId, [FromBody] AreaFiltroProyectoUpdateDto dto)
        {
            try
            {
                await _service.SetFiltroProyectoAsync(areaScopeId, dto?.FiltraPorProyecto ?? false);
                return Ok(new { message = "Configuración del área actualizada exitosamente." });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en AreaRevisorController.SetFiltroProyecto");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
