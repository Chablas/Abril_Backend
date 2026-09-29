using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Filters;

namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Presentation
{
    [ApiController]
    [Route("api/v1/project")]
    [Authorize]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectService _service;

        public ProjectController(IProjectService service)
        {
            _service = service;
        }

        [HttpGet("company-lookup/{ruc}")]
        [EnableRateLimiting("sunat-ruc")]
        public async Task<IActionResult> CompanyLookup(string ruc)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                var result = await _service.GetOrCreateCompanyByRuc(ruc, userId);

                if (result == null)
                    return NotFound(new { message = "No se encontró información para el RUC proporcionado." });

                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Carga inicial de Configuración → Proyectos: catálogos de tipo y ciclo de vida (filtros y
        /// modales) y la primera página. Los cambios de filtro y de página van por <see cref="GetPaged"/>.
        /// </summary>
        [HttpGet("init")]
        public async Task<IActionResult> GetInit(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 200,
            [FromQuery] string? ruc = null,
            [FromQuery] string? razonSocial = null,
            [FromQuery] string? projectDescription = null,
            [FromQuery] bool? active = null,
            [FromQuery] int? projectTipoId = null,
            [FromQuery] int? projectCicloVidaId = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("oid");
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _service.GetInit(page, pageSize, ruc, razonSocial, projectDescription, active, projectTipoId, projectCicloVidaId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 200,
            [FromQuery] string? ruc = null,
            [FromQuery] string? razonSocial = null,
            [FromQuery] string? projectDescription = null,
            [FromQuery] bool? active = null,
            [FromQuery] int? projectTipoId = null,
            [FromQuery] int? projectCicloVidaId = null)
        {
            try
            {
                // Token interno: NameIdentifier = userId. Token Microsoft (AzureAd): oid como fallback.
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("oid");
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var result = await _service.GetPaged(page, pageSize, ruc, razonSocial, projectDescription, active, projectTipoId, projectCicloVidaId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Crear, editar y eliminar proyectos es solo de <see cref="ProyectoRoles.EditanProyectos"/>;
        /// el resto de los roles ve la pantalla en solo lectura. El residente, además, solo lo
        /// asigna quien <see cref="ProyectoRoles.PuedeAsignarResidente"/>.</summary>
        [HttpPost]
        [Authorize(Roles = ProyectoRoles.EditanProyectos)]
        public async Task<IActionResult> Create([FromBody] ProjectCreateDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                if (string.IsNullOrWhiteSpace(dto.ProjectDescription))
                    return BadRequest(new { message = "La descripción del proyecto es obligatoria." });

                var userId = int.Parse(userIdClaim.Value);
                await _service.Create(dto, userId, ProyectoRoles.PuedeAsignarResidente(User));
                return Ok(new { message = "Proyecto creado exitosamente." });
            }
            catch (AbrilException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        [HttpPut]
        [Authorize(Roles = ProyectoRoles.EditanProyectos)]
        public async Task<IActionResult> Update([FromBody] ProjectEditDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                if (string.IsNullOrWhiteSpace(dto.ProjectDescription))
                    return BadRequest(new { message = "La descripción del proyecto es obligatoria." });

                var userId = int.Parse(userIdClaim.Value);
                await _service.Update(dto, userId, ProyectoRoles.PuedeAsignarResidente(User));
                return Ok(new { message = "Proyecto actualizado exitosamente." });
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

        [HttpDelete("{projectId}")]
        [Authorize(Roles = ProyectoRoles.EditanProyectos)]
        public async Task<IActionResult> Delete(int projectId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                var result = await _service.DeleteSoftAsync(projectId, userId);

                if (!result)
                    return NotFound(new { message = "Proyecto no encontrado." });

                return Ok(new { message = "Proyecto eliminado exitosamente." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Proyectos asignados al usuario logueado (tabla `user_project`), para
        /// preseleccionar el proyecto en dashboards que hoy arrancan sin nada elegido.</summary>
        [Authorize]
        [HttpGet("mine")]
        public async Task<IActionResult> GetMine()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                var projectIds = await _service.GetMyProjectIds(userId);
                return Ok(projectIds);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Worker del usuario logueado, mismo cruce User→Person.UserId→Worker.PersonId
        /// que <see cref="GetMine"/>. Usado por Planeamiento BIM para resolver "soy yo el
        /// responsable de este proyecto" sin duplicar el cruce en cada feature.</summary>
        [Authorize]
        [HttpGet("me/worker")]
        public async Task<IActionResult> GetMyWorker()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var userId = int.Parse(userIdClaim.Value);
                var worker = await _service.GetMyWorker(userId);
                if (worker == null)
                    return NotFound(new { message = "Tu usuario no está vinculado a una ficha de trabajador." });

                return Ok(worker);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Los desplegables del modal crear/editar proyecto (responsables Arq. Comercial y
        /// UDP + elegibles como residente y coordinador administrativo) en una sola petición.
        /// </summary>
        [Authorize]
        [HttpGet("lookups")]
        public async Task<IActionResult> GetLookups()
        {
            try
            {
                var result = await _service.GetLookups();
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

        /// <summary>
        /// Activa/desactiva rápidamente si el proyecto aparece en los selectores de
        /// Arquitectura Comercial (Observaciones, etc.) sin pasar por el formulario
        /// completo de edición de proyecto.
        /// </summary>
        [Authorize]
        [RequireFeature("arquitectura-comercial.observaciones")]
        [HttpPatch("{id}/arquitectura-comercial")]
        public async Task<IActionResult> ToggleArquitecturaComercial(int id)
        {
            try
            {
                var nuevoValor = await _service.ToggleArquitecturaComercial(id);
                if (nuevoValor == null)
                    return NotFound(new { message = "Proyecto no encontrado." });

                return Ok(new { tieneArquitecturaComercial = nuevoValor.Value });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Activa/desactiva si el proyecto pertenece al módulo Unidad de Proyectos
        /// (Cronograma de Actividades, Actas de Reunión, Dashboard UDP, etc.) sin pasar
        /// por el PUT completo de edición — evita el riesgo de sobreescribir campos no
        /// enviados que tiene reconstruir un ProjectEditDto completo solo para este flag.
        /// </summary>
        [Authorize]
        [RequireFeature("projects.config.milestones")]
        [HttpPatch("{id}/tiene-unidad-de-proyectos")]
        public async Task<IActionResult> UpdateTieneUnidadDeProyectos(int id, [FromBody] UpdateTieneUnidadDeProyectosDto dto)
        {
            try
            {
                var nuevoValor = await _service.SetTieneUnidadDeProyectos(id, dto.Value);
                if (nuevoValor == null)
                    return NotFound(new { message = "Proyecto no encontrado." });

                return Ok(new { tieneUnidadDeProyectos = nuevoValor.Value });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Torres/bloques del proyecto (A, B, C...) con su cantidad de sótanos/pisos/
        /// cisternas — de ahí RAC/ATS arman el selector de "Lugar" en vez de texto libre.</summary>
        [Authorize]
        [HttpGet("{id}/torres")]
        public async Task<IActionResult> GetTorres(int id)
        {
            try
            {
                return Ok(await _service.GetTorres(id));
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>Las torres son datos del proyecto: las cambian los mismos roles que lo editan
        /// (<see cref="ProyectoRoles.EditanProyectos"/>); el resto las ve en solo lectura.</summary>
        [Authorize(Roles = ProyectoRoles.EditanProyectos)]
        [HttpPut("{id}/torres")]
        public async Task<IActionResult> SetTorres(int id, [FromBody] List<ProjectTorreGuardarDto> torres)
        {
            try
            {
                await _service.SetTorres(id, torres);
                return Ok(new { message = "Torres actualizadas correctamente." });
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
    }
}
