using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionAdministrativa.PlazoRendicion.Presentation
{
    /// <summary>
    /// Sección "Días reembolsables" de Mis Rendiciones → Configuración: cuántos días hábiles del mes
    /// siguiente dura el plazo para rendir un mes. Cuelga de <c>rendiciones/configuracion</c> igual
    /// que los correos de esa misma pantalla.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/rendiciones/configuracion/plazo")]
    [Authorize]
    public class PlazoRendicionController : ControllerBase
    {
        private readonly IPlazoRendicionService _service;
        private readonly ILogger<PlazoRendicionController> _logger;

        public PlazoRendicionController(IPlazoRendicionService service, ILogger<PlazoRendicionController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        [HttpGet]
        public Task<IActionResult> Get() =>
            Ejecutar(nameof(Get), async () => Ok(await _service.Get()));

        [HttpPut]
        public Task<IActionResult> Save([FromBody] PlazoRendicionSaveDto dto) =>
            Ejecutar(nameof(Save), async () =>
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión" });

                var actualizado = await _service.Save(dto ?? new PlazoRendicionSaveDto(), userId.Value);
                return Ok(new { plazo = actualizado, message = "Plazo de rendición actualizado exitosamente." });
            });

        private int? GetUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        private async Task<IActionResult> Ejecutar(string accion, Func<Task<IActionResult>> operacion)
        {
            try { return await operacion(); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PlazoRendicionController.{Accion}", accion);
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
