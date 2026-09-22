using Abril_Backend.Application.Exceptions;
using Abril_Backend.Shared.Services.Firma.Dtos;
using Abril_Backend.Shared.Services.Firma.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Abril_Backend.Features.MiPerfilModule.Features.MiFirmaFeature.Presentation
{
    /// <summary>
    /// Mi Perfil → Mi Firma: las firmas del usuario que está logueado. Es a propósito el mismo
    /// endpoint para todos: una persona tiene una firma POR TIPO (<c>person_firma</c> →
    /// <c>firma_tipo</c>: dibujada con el mouse y/o subida como imagen), las registre desde donde
    /// las registre (Mi Perfil → Mi Firma, Contabilidad → Configuración → Firma, o el modal que salta
    /// al firmar sin tener firma), y esas mismas firmas son las que se estampan en las facturas, en
    /// la carta oferta y en la planilla de rendición de salidas.
    ///
    /// El GET devuelve también qué tipos están habilitados, porque quien pide la firma necesita
    /// saber en el mismo viaje si tiene que mostrar el lienzo, el selector de imagen o los dos.
    ///
    /// Sin restricción de rol: cualquier usuario autenticado registra la suya y solo la suya — el
    /// user id sale del token, nunca de la petición.
    ///
    /// Vivía en <c>api/v1/configuracion/mi-firma</c> (ConfigurationModule) mientras la firma se
    /// registraba desde Gestión Administrativa → Configuración → Tu firma; se mudó con la pantalla
    /// al perfil del usuario (2026-09-22).
    /// </summary>
    [ApiController]
    [Route("api/v1/mi-perfil/mi-firma")]
    [Authorize]
    public class MiFirmaController : ControllerBase
    {
        private readonly IFirmaPersonalService _service;
        private readonly ILogger<MiFirmaController> _logger;

        public MiFirmaController(IFirmaPersonalService service, ILogger<MiFirmaController> logger)
        {
            _service = service;
            _logger  = logger;
        }

        /// <summary>
        /// Tipos de firma habilitados y las firmas que el usuario actual ya registró (lista vacía si
        /// todavía no registró ninguna).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión" });

                return Ok(await _service.GetEstado(userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en MiFirmaController.Get");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        /// <summary>
        /// Guarda/actualiza la firma del usuario actual para un tipo (el PNG dibujado en el canvas o
        /// la imagen que subió) y devuelve el estado completo ya actualizado.
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> Save([FromBody] FirmaPersonalSaveDto dto)
        {
            try
            {
                var userId = GetUserId();
                if (userId == null) return Unauthorized(new { message = "Inicie sesión" });

                return Ok(await _service.Save(dto, userId.Value));
            }
            catch (AbrilException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en MiFirmaController.Save");
                return StatusCode(500, new { message = "Error del servidor. Por favor contactar al administrador del sistema." });
            }
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : null;
        }
    }
}
