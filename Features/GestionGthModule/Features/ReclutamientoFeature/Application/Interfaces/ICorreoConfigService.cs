using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces
{
    /// <summary>
    /// Pantalla de Configuración de correos de Solicitud de Personal: una sección por cada
    /// correo del flujo, con su interruptor maestro y sus destinatarios (dinámicos del catálogo
    /// + correos adicionales escritos a mano).
    /// </summary>
    public interface ICorreoConfigService
    {
        /// <summary>Los correos del flujo de solicitud de personal con todos sus destinatarios.</summary>
        Task<CorreoConfigDto> GetConfig();

        /// <summary>Agrega un correo adicional a un correo. Devuelve el id creado.</summary>
        Task<int> CrearAdicional(CorreoAdicionalCreateDto dto, int? userId);

        /// <summary>Edita un correo adicional.</summary>
        Task ActualizarAdicional(int destinatarioId, CorreoAdicionalUpdateDto dto, int? userId);

        /// <summary>Prende o apaga un destinatario.</summary>
        Task SetDestinatarioActive(int destinatarioId, bool active, int? userId);

        /// <summary>Prende o apaga un correo completo.</summary>
        Task SetCorreoActive(string tipoSlug, bool active, int? userId);

        /// <summary>Elimina (baja lógica) un correo adicional.</summary>
        Task EliminarAdicional(int destinatarioId, int? userId);
    }
}
