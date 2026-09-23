using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.GestionAdministrativa.Archivos.Application;
using Abril_Backend.Features.GestionAdministrativa.Archivos.Application.Interfaces;
using Abril_Backend.Shared.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.GestionAdministrativa.Archivos.Presentation
{
    /// <summary>
    /// Los archivos de las siete pantallas de salidas, para los modales que los muestran embebidos
    /// debajo de su enlace. Entra quien tenga alguna de esas pantallas; qué archivo puede ver lo
    /// decide el servicio.
    /// </summary>
    [ApiController]
    [Route("api/v1/gestion-administrativa/archivos")]
    [Authorize]
    [RequireFeature(
        PantallasSalidas.SolicitudSalidas, PantallasSalidas.Rendiciones,
        PantallasSalidas.GestionSalidas, PantallasSalidas.GestionRendiciones,
        PantallasSalidas.Consolidados, PantallasSalidas.CorreccionesS10,
        PantallasSalidas.Reembolsos)]
    public class ArchivoSalidaController : ControllerBase
    {
        private readonly IArchivoSalidaService _service;
        private readonly ILogger<ArchivoSalidaController> _logger;

        public ArchivoSalidaController(IArchivoSalidaService service, ILogger<ArchivoSalidaController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <param name="url">El webUrl tal como lo guarda la base (y lo trae el DTO del modal).</param>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string url)
        {
            try
            {
                if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                    return Unauthorized(new { message = "Usuario no autenticado." });

                var roleIds = User.FindAll(ClaimTypes.Role)
                    .Select(c => int.TryParse(c.Value, out var id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToArray();

                var archivo = await _service.Get(url, userId, roleIds);
                return File(archivo.Contenido, archivo.ContentType);
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ArchivoSalidaController.Get");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }
    }
}
