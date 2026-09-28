using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Presentation
{
    [ApiController]
    [Route("api/v1/cronograma-actividades/plantillas")]
    [Authorize]
    public class PlantillaCronogramaController : ControllerBase
    {
        private readonly IPlantillaCronogramaService _service;

        public PlantillaCronogramaController(IPlantillaCronogramaService service)
        {
            _service = service;
        }

        private int GetUserId() =>
            int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        // GET /api/v1/cronograma-actividades/plantillas/{tipoCronograma}
        [HttpGet("{tipoCronograma}")]
        public async Task<IActionResult> GetPorTipo(string tipoCronograma)
        {
            try
            {
                var items = await _service.GetByTipoAsync(tipoCronograma);
                return Ok(new PlantillaDto { TipoCronograma = tipoCronograma, Items = items });
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // POST /api/v1/cronograma-actividades/plantillas
        [HttpPost]
        public async Task<IActionResult> CrearItem([FromBody] CrearPlantillaItemRequest request)
        {
            try
            {
                var result = await _service.CrearItemAsync(request, GetUserId());
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // PUT /api/v1/cronograma-actividades/plantillas/{id}
        [HttpPut("{id:int}")]
        public async Task<IActionResult> EditarItem(int id, [FromBody] EditarPlantillaItemRequest request)
        {
            try
            {
                var result = await _service.EditarItemAsync(id, request, GetUserId());
                return Ok(result);
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        // DELETE /api/v1/cronograma-actividades/plantillas/{id}
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> EliminarItem(int id)
        {
            try
            {
                await _service.EliminarItemAsync(id, GetUserId());
                return NoContent();
            }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
