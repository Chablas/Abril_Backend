using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Presentation
{
    /// <summary>
    /// El único endpoint de los recordatorios del plazo de rendición (RG-33 y RG-34). Lo llama el
    /// cron externo UNA VEZ POR DÍA y siempre a la misma hora: qué recordatorio corresponde —o
    /// ninguno— lo decide el servicio contra el calendario, no la programación del cron.
    ///
    /// Es anónimo pero exige el <c>CronSecret</c> en el Authorization, igual que el resto de los
    /// endpoints de cron (alertas de habilitación, recordatorios de evaluaciones): el cron no tiene
    /// sesión de usuario, y sin el secreto cualquiera podría disparar el envío masivo.
    ///
    /// Responde 200 siempre que la corrida haya terminado, aunque no haya enviado nada: el cuerpo
    /// dice qué pasó y es lo único que queda en el log del cron.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/recordatorios/rendicion")]
    public class RecordatorioRendicionController : ControllerBase
    {
        private readonly IRecordatorioRendicionService _service;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RecordatorioRendicionController> _logger;

        public RecordatorioRendicionController(
            IRecordatorioRendicionService service,
            IConfiguration configuration,
            ILogger<RecordatorioRendicionController> logger)
        {
            _service        = service;
            _configuration  = configuration;
            _logger         = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Ejecutar()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (authHeader != $"Bearer {_configuration["CronSecret"]}")
                    return Unauthorized();

                var resultado = await _service.EjecutarAsync();
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RecordatorioRendicionController.Ejecutar");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
