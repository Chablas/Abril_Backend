using System.Security.Claims;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Constants;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Presentation
{
    /// <summary>
    /// Cronograma de Hitos → Configuración: los correos y recordatorios del cronograma y a quién le
    /// llegan. Solo con la funcionalidad de configuración. Las escrituras son una por acción de la
    /// pantalla (los interruptores guardan al tocarlos); las que cambian la lista devuelven el
    /// correo actualizado.
    /// </summary>
    [ApiController]
    [Route("api/v1/milestone-schedule/configuracion")]
    [Authorize]
    [RequireFeature(CronogramaHitosFeatures.Configuracion)]
    public class CronogramaConfiguracionController : ControllerBase
    {
        private readonly ICronogramaConfiguracionService _service;
        private readonly ILogger<CronogramaConfiguracionController> _logger;

        public CronogramaConfiguracionController(
            ICronogramaConfiguracionService service,
            ILogger<CronogramaConfiguracionController> logger)
        {
            _service = service;
            _logger = logger;
        }

        private int UserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        /// <summary>Toda la pantalla en una llamada: secciones, correos, destinatarios y opciones.</summary>
        [HttpGet]
        public Task<IActionResult> Get() =>
            Ejecutar(nameof(Get), async () => Ok(await _service.GetAsync()));

        /// <summary>Interruptor del correo.</summary>
        [HttpPut("correos/{codigo}/active")]
        public Task<IActionResult> SetCorreoActive(string codigo, [FromBody] CronogramaCorreoActiveDto dto) =>
            Ejecutar(nameof(SetCorreoActive), async () =>
            {
                await _service.SetCorreoActiveAsync(codigo, dto?.Active ?? false, UserId());
                return Ok(new { message = "Correo actualizado." });
            });

        /// <summary>Interruptor del destinatario que pone el sistema (el residente).</summary>
        [HttpPut("correos/{codigo}/principal/active")]
        public Task<IActionResult> SetPrincipalActive(string codigo, [FromBody] CronogramaCorreoActiveDto dto) =>
            Ejecutar(nameof(SetPrincipalActive), async () =>
            {
                await _service.SetPrincipalActiveAsync(codigo, dto?.Active ?? false, UserId());
                return Ok(new { message = "Destinatario actualizado." });
            });

        [HttpPost("correos/{codigo}/destinatarios")]
        public Task<IActionResult> CrearDestinatario(string codigo, [FromBody] CronogramaCorreoDestinatarioInputDto dto) =>
            Ejecutar(nameof(CrearDestinatario), async () =>
                Ok(await _service.CrearDestinatarioAsync(codigo, dto, UserId())));

        [HttpPut("destinatarios/{id:int}")]
        public Task<IActionResult> ActualizarDestinatario(int id, [FromBody] CronogramaCorreoDestinatarioInputDto dto) =>
            Ejecutar(nameof(ActualizarDestinatario), async () =>
                Ok(await _service.ActualizarDestinatarioAsync(id, dto, UserId())));

        [HttpPut("destinatarios/{id:int}/active")]
        public Task<IActionResult> SetDestinatarioActive(int id, [FromBody] CronogramaCorreoActiveDto dto) =>
            Ejecutar(nameof(SetDestinatarioActive), async () =>
            {
                await _service.SetDestinatarioActiveAsync(id, dto?.Active ?? false, UserId());
                return Ok(new { message = "Destinatario actualizado." });
            });

        [HttpDelete("destinatarios/{id:int}")]
        public Task<IActionResult> EliminarDestinatario(int id) =>
            Ejecutar(nameof(EliminarDestinatario), async () =>
                Ok(await _service.EliminarDestinatarioAsync(id, UserId())));

        /// <summary>
        /// Mismo manejo en todas las acciones: AbrilException conserva su código y su mensaje (la
        /// pantalla los muestra) y cualquier otra excepción se registra y sale como 500.
        /// </summary>
        private async Task<IActionResult> Ejecutar(string accion, Func<Task<IActionResult>> operacion)
        {
            try { return await operacion(); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CronogramaConfiguracionController.{Accion}", accion);
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
