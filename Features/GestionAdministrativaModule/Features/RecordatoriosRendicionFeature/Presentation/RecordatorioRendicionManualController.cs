using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Presentation
{
    /// <summary>
    /// Envío manual de los recordatorios del plazo (Solicitud de Salidas → Configuración →
    /// Recordatorios): se simula un día y sale lo que el cron mandaría ese día, o nada. Va aparte del
    /// controller del cron, que es anónimo con CronSecret: este exige sesión y la misma funcionalidad
    /// que administra esos recordatorios.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/solicitud-salidas/configuracion/recordatorios")]
    [Authorize]
    [RequireFeature("gestion-administrativa.config.correos")]
    public class RecordatorioRendicionManualController : ControllerBase
    {
        private readonly IRecordatorioRendicionService _service;
        private readonly ILogger<RecordatorioRendicionManualController> _logger;

        public RecordatorioRendicionManualController(
            IRecordatorioRendicionService service,
            ILogger<RecordatorioRendicionManualController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Paso 1: a quién le saldría si el cron corriera el día <paramref name="fecha"/>
        /// (yyyy-MM-dd, hora de Perú). No envía nada.
        /// </summary>
        [HttpGet("{eventoCodigo}/simulacion")]
        public Task<IActionResult> Simular(string eventoCodigo, [FromQuery] DateOnly? fecha) =>
            Ejecutar(nameof(Simular), async () =>
                Ok(await _service.SimularAsync(eventoCodigo, Fecha(fecha))));

        /// <summary>Paso 2: manda lo que saldría ese día. Si ese día no sale nada, no manda nada.</summary>
        [HttpPost("{eventoCodigo}/envio-manual")]
        public Task<IActionResult> EnviarManual(string eventoCodigo, [FromQuery] DateOnly? fecha) =>
            Ejecutar(nameof(EnviarManual), async () =>
                Ok(await _service.EnviarManualAsync(eventoCodigo, Fecha(fecha))));

        private static DateOnly Fecha(DateOnly? fecha) =>
            fecha ?? throw new AbrilException("Elige el día a simular.", 400);

        private async Task<IActionResult> Ejecutar(string accion, Func<Task<IActionResult>> operacion)
        {
            try { return await operacion(); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RecordatorioRendicionManualController.{Accion}", accion);
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
