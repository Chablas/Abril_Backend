using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Presentation
{
    [ApiController]
    [Route("api/v1/cronograma-actividades")]
    [Authorize]
    public class CronogramaActividadesController : ControllerBase
    {
        private readonly ICronogramaActividadesService _service;

        public CronogramaActividadesController(ICronogramaActividadesService service)
        {
            _service = service;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        // GET /api/v1/cronograma-actividades/proyectos
        [HttpGet("proyectos")]
        public async Task<IActionResult> GetProyectos()
        {
            try
            {
                var result = await _service.GetProyectosAsync();
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // GET /api/v1/cronograma-actividades/{proyectoId}/actividades
        [HttpGet("{proyectoId:int}/actividades")]
        public async Task<IActionResult> GetActividades(int proyectoId)
        {
            try
            {
                var result = await _service.GetActividadesAsync(proyectoId);
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // POST /api/v1/cronograma-actividades/{proyectoId}/actividades
        [HttpPost("{proyectoId:int}/actividades")]
        public async Task<IActionResult> CrearActividad(int proyectoId, [FromBody] CrearActividadRequest request)
        {
            try
            {
                var result = await _service.CrearActividadAsync(proyectoId, request, GetUserId());
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // PUT /api/v1/cronograma-actividades/actividades/{projectActivityId}
        [HttpPut("actividades/{projectActivityId:int}")]
        public async Task<IActionResult> EditarActividad(int projectActivityId, [FromBody] EditarActividadRequest request)
        {
            try
            {
                var result = await _service.EditarActividadAsync(projectActivityId, request, GetUserId());
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // PATCH /api/v1/cronograma-actividades/actividades/{projectActivityId}/culminar
        [HttpPatch("actividades/{projectActivityId:int}/culminar")]
        public async Task<IActionResult> CulminarActividad(int projectActivityId)
        {
            try
            {
                var result = await _service.CulminarActividadAsync(projectActivityId, GetUserId());
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // DELETE /api/v1/cronograma-actividades/actividades/{projectActivityId}
        [HttpDelete("actividades/{projectActivityId:int}")]
        public async Task<IActionResult> EliminarActividad(int projectActivityId)
        {
            try
            {
                await _service.EliminarActividadAsync(projectActivityId, GetUserId());
                return NoContent();
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
