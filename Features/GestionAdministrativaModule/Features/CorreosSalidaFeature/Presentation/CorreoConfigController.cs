using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Presentation
{
    /// <summary>
    /// Configuración de los correos de salidas, una por pantalla del flujo: <c>{pantalla}</c> es el
    /// mismo segmento que la URL del frontend (solicitud-salidas, rendiciones, gestion-salidas,
    /// gestion-rendiciones, reembolsos) y acota todo lo que se lee y se escribe a los correos que
    /// se originan ahí. Es el mismo esquema que la configuración de correos de Gestión GTH
    /// (<c>api/v1/gestion-gth/{modulo}/configuracion</c>), que reemplazó a la pantalla única
    /// Configuración → Correos.
    ///
    /// Las escrituras son granulares (una por acción de la pantalla): los interruptores guardan al
    /// momento de tocarlos.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/{pantalla}/configuracion/correos")]
    [Authorize]
    public class CorreoConfigController : ControllerBase
    {
        private readonly ICorreoConfigService _service;
        private readonly ILogger<CorreoConfigController> _logger;

        public CorreoConfigController(ICorreoConfigService service, ILogger<CorreoConfigController> logger)
        {
            _service = service;
            _logger = logger;
        }

        /// <summary>
        /// Carga inicial: los correos de una sección de la pantalla con sus destinatarios +
        /// opciones de los desplegables. <paramref name="grupo"/> es la sección
        /// (ga_correo_grupo): omitido = "correos" (los del flujo), "recordatorios" = los del plazo
        /// de rendición. Va como query y no como segmento de la ruta para no romper las llamadas
        /// de las cinco configuraciones que existían antes de que hubiera secciones.
        /// </summary>
        [HttpGet]
        public Task<IActionResult> GetInicial(string pantalla, [FromQuery] string? grupo = null) =>
            Ejecutar(nameof(GetInicial), async () => Ok(await _service.GetInicialAsync(pantalla, grupo)));

        /// <summary>Interruptor maestro del correo.</summary>
        [HttpPut("{eventoCodigo}/active")]
        public Task<IActionResult> SetEventoActive(string pantalla, string eventoCodigo, [FromBody] CorreoActiveUpdateDto dto) =>
            Ejecutar(nameof(SetEventoActive), async () =>
            {
                await _service.SetEventoActiveAsync(pantalla, eventoCodigo, dto?.Active ?? false);
                return Ok(new { message = "Correo actualizado exitosamente." });
            });

        /// <summary>Interruptor del destinatario principal (el revisor, el solicitante).</summary>
        [HttpPut("{eventoCodigo}/principal/active")]
        public Task<IActionResult> SetPrincipalActive(string pantalla, string eventoCodigo, [FromBody] CorreoActiveUpdateDto dto) =>
            Ejecutar(nameof(SetPrincipalActive), async () =>
            {
                await _service.SetPrincipalActiveAsync(pantalla, eventoCodigo, dto?.Active ?? false);
                return Ok(new { message = "Destinatario principal actualizado exitosamente." });
            });

        /// <summary>Agrega un destinatario al correo.</summary>
        [HttpPost("{eventoCodigo}/destinatarios")]
        public Task<IActionResult> CrearDestinatario(string pantalla, string eventoCodigo, [FromBody] CorreoDestinatarioInputDto dto) =>
            Ejecutar(nameof(CrearDestinatario), async () =>
            {
                var id = await _service.CrearDestinatarioAsync(pantalla, eventoCodigo, dto ?? new CorreoDestinatarioInputDto());
                return Ok(new { id, message = "Destinatario agregado exitosamente." });
            });

        /// <summary>Cambia a quién apunta un destinatario ya configurado.</summary>
        [HttpPut("destinatarios/{id:int}")]
        public Task<IActionResult> ActualizarDestinatario(string pantalla, int id, [FromBody] CorreoDestinatarioInputDto dto) =>
            Ejecutar(nameof(ActualizarDestinatario), async () =>
            {
                await _service.ActualizarDestinatarioAsync(pantalla, id, dto ?? new CorreoDestinatarioInputDto());
                return Ok(new { message = "Destinatario actualizado exitosamente." });
            });

        /// <summary>Prende o apaga un destinatario sin borrarlo.</summary>
        [HttpPut("destinatarios/{id:int}/active")]
        public Task<IActionResult> SetDestinatarioActive(string pantalla, int id, [FromBody] CorreoActiveUpdateDto dto) =>
            Ejecutar(nameof(SetDestinatarioActive), async () =>
            {
                await _service.SetDestinatarioActiveAsync(pantalla, id, dto?.Active ?? false);
                return Ok(new { message = "Destinatario actualizado exitosamente." });
            });

        /// <summary>Da de baja un destinatario.</summary>
        [HttpDelete("destinatarios/{id:int}")]
        public Task<IActionResult> EliminarDestinatario(string pantalla, int id) =>
            Ejecutar(nameof(EliminarDestinatario), async () =>
            {
                await _service.EliminarDestinatarioAsync(pantalla, id);
                return Ok(new { message = "Destinatario eliminado exitosamente." });
            });

        /// <summary>
        /// Mismo manejo de errores en las seis acciones: AbrilException conserva su código y su
        /// mensaje (la pantalla los muestra), y cualquier otra excepción se registra y sale como
        /// 500 genérico.
        /// </summary>
        private async Task<IActionResult> Ejecutar(string accion, Func<Task<IActionResult>> operacion)
        {
            try { return await operacion(); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en CorreoConfigController.{Accion}", accion);
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
