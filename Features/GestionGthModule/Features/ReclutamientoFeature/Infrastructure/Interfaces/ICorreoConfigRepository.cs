using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces
{
    /// <summary>
    /// Configuración de los correos de Reclutamiento: qué correos existen, si están prendidos
    /// y quién recibe cada uno. Sirve a la pantalla de Configuración (lectura completa + CRUD
    /// de correos adicionales) y al envío real, que consume solo las filas activas.
    /// </summary>
    public interface ICorreoConfigRepository
    {
        /// <summary>
        /// Los correos indicados con su interruptor y todos sus destinatarios, resolviendo de
        /// paso los dinámicos que no dependen de la solicitud (Gerente General y área de GTH)
        /// para que la pantalla muestre a quién le llega de verdad. Una sola llamada.
        /// </summary>
        Task<CorreoConfigDto> GetConfigAsync(IReadOnlyList<string> tipoCodigos);

        /// <summary>
        /// Destinatarios vigentes y activos de un correo activo. Lista vacía si el correo está
        /// apagado (interruptor maestro) o no existe.
        /// </summary>
        Task<List<CorreoDestinatarioEnvioDto>> GetDestinatariosEnvioAsync(string tipoCodigo);

        /// <summary>Gerente General vigente (puesto "GERENTE GENERAL"); null si no hay uno con correo.</summary>
        Task<CorreoDestinatarioResueltoDto?> GetGerenteGeneralAsync();

        /// <summary>Correo del área de Gestión del Talento Humano; null si el área no tiene uno cargado.</summary>
        Task<string?> GetEmailAreaGthAsync();

        /// <summary>Alta de un correo adicional en un correo. Devuelve el id creado.</summary>
        Task<int> CreateAdicionalAsync(string tipoCodigo, string email, string? nombre, bool esCopia, int? userId);

        /// <summary>Edición de un correo adicional (los dinámicos no se editan).</summary>
        Task UpdateAdicionalAsync(int destinatarioId, string email, string? nombre, bool esCopia, int? userId);

        /// <summary>Prende o apaga un destinatario.</summary>
        Task SetDestinatarioActiveAsync(int destinatarioId, bool active, int? userId);

        /// <summary>Prende o apaga un correo completo (interruptor maestro).</summary>
        Task SetTipoActiveAsync(string tipoCodigo, bool active, int? userId);

        /// <summary>Baja lógica de un correo adicional (los dinámicos no se eliminan, se apagan).</summary>
        Task DeleteAdicionalAsync(int destinatarioId, int? userId);
    }
}
