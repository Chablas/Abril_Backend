using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;
using Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces;
using Abril_Backend.Shared.Constants;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Presentation
{
    [ApiController]
    [Route("api/v1/planeamiento-bim/restricciones")]
    [Authorize]
    [RequireFeature("planeamiento-bim.configuracion-inicial")]
    public class PlaneamientoBimRestriccionController : ControllerBase
    {
        private readonly IPlaneamientoBimRestriccionService _service;
        private readonly IPlaneamientoBimAccesoService _acceso;

        public PlaneamientoBimRestriccionController(IPlaneamientoBimRestriccionService service, IPlaneamientoBimAccesoService acceso)
        {
            _service = service;
            _acceso = acceso;
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("oid");
            return claim != null ? int.Parse(claim.Value) : null;
        }

        private bool EsAdmin() => User.IsInRole(Roles.AdministradorSistema) || User.IsInRole(Roles.AdministradorUdp);
        private bool EsPlaneamientoUdp() => User.IsInRole(Roles.PlaneamientoUdp);

        [HttpGet("{projectId:int}")]
        public async Task<IActionResult> GetPaged(int projectId, [FromQuery] bool? soloActivos)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                await _acceso.ValidarAccesoProyecto(userId.Value, projectId, EsAdmin(), EsPlaneamientoUdp());

                return Ok(await _service.GetPaged(projectId, soloActivos));
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

        [HttpPost("{projectId:int}")]
        public async Task<IActionResult> Create(int projectId, [FromBody] RestriccionCreateDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                await _acceso.ValidarAccesoProyecto(userId.Value, projectId, EsAdmin(), EsPlaneamientoUdp());

                var result = await _service.Create(projectId, dto, userId.Value);
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

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] RestriccionUpdateDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var projectId = await _acceso.ResolverProjectIdDeRestriccion(id);
                await _acceso.ValidarAccesoProyecto(userId.Value, projectId, EsAdmin(), EsPlaneamientoUdp());

                return Ok(await _service.Update(id, dto));
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

        [HttpPut("{id:int}/cerrar")]
        public async Task<IActionResult> Cerrar(int id)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null)
                    return Unauthorized(new { message = "Inicie sesión" });

                var projectId = await _acceso.ResolverProjectIdDeRestriccion(id);
                await _acceso.ValidarAccesoProyecto(userId.Value, projectId, EsAdmin(), EsPlaneamientoUdp());

                return Ok(await _service.Cerrar(id));
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
