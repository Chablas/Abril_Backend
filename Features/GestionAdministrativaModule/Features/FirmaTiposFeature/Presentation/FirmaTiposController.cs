using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Services.Firma.Dtos;
using Abril_Backend.Shared.Services.Firma.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionAdministrativa.FirmaTipos.Presentation
{
    /// <summary>
    /// Sección "Firmas" de Consolidados → Configuración: los dos checkboxes que deciden cómo se
    /// registra la firma que el jefe estampa al aprobar un consolidado —subiendo una imagen,
    /// dibujándola con el mouse, o cualquiera de las dos.
    ///
    /// Cuelga de <c>consolidados/configuracion</c> igual que los correos, la visibilidad y los
    /// consolidadores: la regla se hace valer al aprobar un consolidado, y es la única pantalla que
    /// la honra. Contabilidad → Firma y Gestión Administrativa → Tu firma siguen ofreciendo solo el
    /// dibujo, así que apagar un tipo no les cambia nada.
    ///
    /// El catálogo que administra (<c>firma_tipo</c>) sí es global —lo referencian las firmas de
    /// todos los módulos—, por eso el servicio vive en <c>Shared/Services/Firma</c> y acá queda
    /// solo el endpoint de la pantalla que lo administra.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/consolidados/configuracion/firmas")]
    [Authorize]
    public class FirmaTiposController : ControllerBase
    {
        private readonly IFirmaPersonalService _service;
        private readonly ILogger<FirmaTiposController> _logger;

        public FirmaTiposController(IFirmaPersonalService service, ILogger<FirmaTiposController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>Los tipos de firma con su bandera de habilitado.</summary>
        [HttpGet]
        public Task<IActionResult> Get() =>
            Ejecutar(nameof(Get), async () => Ok(new { tipos = await _service.GetTipos() }));

        /// <summary>Guarda qué tipos quedan habilitados (al menos uno).</summary>
        [HttpPut]
        public Task<IActionResult> Save([FromBody] FirmaTiposSaveDto dto) =>
            Ejecutar(nameof(Save), async () =>
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión" });

                var tipos = await _service.SaveTipos(dto ?? new FirmaTiposSaveDto(), userId.Value);
                return Ok(new { tipos, message = "Tipos de firma actualizados exitosamente." });
            });

        private int? GetUserId() =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

        private async Task<IActionResult> Ejecutar(string accion, Func<Task<IActionResult>> operacion)
        {
            try { return await operacion(); }
            catch (AbrilException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en FirmaTiposController.{Accion}", accion);
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
