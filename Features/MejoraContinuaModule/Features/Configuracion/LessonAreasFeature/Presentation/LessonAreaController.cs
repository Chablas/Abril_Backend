using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonAreasFeature.Presentation
{
    [ApiController]
    [Route("api/v1/mejora-continua/lesson-areas")]
    public class LessonAreaController : ControllerBase
    {
        private readonly ILessonAreaService _service;
        public LessonAreaController(ILessonAreaService service) => _service = service;

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try { return Ok(await _service.GetAllAsync()); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }

        [Authorize]
        [HttpPut("toggle/{areaItemId}")]
        public async Task<IActionResult> Toggle(int areaItemId)
        {
            try { return Ok(await _service.ToggleAsync(areaItemId)); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception) { return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." }); }
        }
    }
}
