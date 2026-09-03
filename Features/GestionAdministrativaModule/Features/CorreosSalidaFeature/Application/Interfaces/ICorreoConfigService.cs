using Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.CorreosSalida.Application.Interfaces
{
    /// <summary>
    /// Configuración de los destinatarios de los correos del flujo de solicitud de salidas.
    /// Las operaciones son granulares (una por acción de la pantalla): los interruptores guardan
    /// al momento de tocarlos, en vez de acumular cambios hasta un botón "Guardar".
    /// </summary>
    public interface ICorreoConfigService
    {
        /// <summary>Carga inicial de la pantalla (correos + destinatarios + opciones) en una sola llamada.</summary>
        Task<CorreoConfigInicialDto> GetInicialAsync();

        /// <summary>Interruptor maestro del correo: apagado, no se envía a nadie.</summary>
        Task SetEventoActiveAsync(string eventoCodigo, bool active);

        /// <summary>Interruptor del destinatario principal (el revisor, el solicitante).</summary>
        Task SetPrincipalActiveAsync(string eventoCodigo, bool active);

        /// <summary>Agrega un destinatario al correo. Devuelve el id de la regla creada.</summary>
        Task<int> CrearDestinatarioAsync(string eventoCodigo, CorreoDestinatarioInputDto dto);

        /// <summary>Cambia a quién apunta un destinatario ya configurado.</summary>
        Task ActualizarDestinatarioAsync(int id, CorreoDestinatarioInputDto dto);

        /// <summary>Prende o apaga un destinatario sin borrarlo.</summary>
        Task SetDestinatarioActiveAsync(int id, bool active);

        /// <summary>Da de baja un destinatario (soft delete: se conserva para auditoría).</summary>
        Task EliminarDestinatarioAsync(int id);
    }
}
