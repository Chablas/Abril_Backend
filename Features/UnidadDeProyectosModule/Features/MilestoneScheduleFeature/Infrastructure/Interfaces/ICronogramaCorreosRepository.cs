using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces
{
    /// <summary>
    /// Correos del Cronograma de Hitos (<c>milestone_schedule_correo</c> y sus destinatarios): la
    /// pantalla de configuración, que guarda al tocar cada control, y la lista de cada envío.
    /// </summary>
    public interface ICronogramaCorreosRepository
    {
        /// <summary>Toda la pantalla (secciones, correos, destinatarios y opciones) en un solo viaje.</summary>
        Task<CronogramaConfiguracionDto> GetConfiguracionAsync();

        /// <summary>false = no existe el correo.</summary>
        Task<bool> SetCorreoActiveAsync(string codigo, bool active, int userId);

        /// <summary>false = no existe el correo o no tiene destinatario del sistema.</summary>
        Task<bool> SetPrincipalActiveAsync(string codigo, bool active, int userId);

        /// <summary>
        /// Agrega un destinatario ya validado (uno solo de worker, rol o correo) y devuelve el
        /// correo con su lista actualizada. Un destinatario repetido sale como 409.
        /// </summary>
        Task<CronogramaDestinatarioGuardadoDto> CrearDestinatarioAsync(
            string codigo, string tipoCodigo, string recepcionCodigo, int? workerId, int? roleId, string? correo, int userId);

        /// <summary>Cambia a quién apunta un destinatario o cómo lo recibe. Mismo resultado que el alta.</summary>
        Task<CronogramaDestinatarioGuardadoDto> ActualizarDestinatarioAsync(
            int id, string tipoCodigo, string recepcionCodigo, int? workerId, int? roleId, string? correo, int userId);

        /// <summary>false = no existe el destinatario.</summary>
        Task<bool> SetDestinatarioActiveAsync(int id, bool active, int userId);

        /// <summary>Baja lógica. Devuelve su correo con la lista actualizada; null si no existía.</summary>
        Task<CronogramaCorreoDto?> EliminarDestinatarioAsync(int id, int userId);

        /// <summary>
        /// Lo que hace falta para enviar el correo: sus interruptores y las direcciones de sus
        /// destinatarios prendidos (un rol, ya expandido a quienes lo tienen hoy). Null si el correo
        /// no está en la base.
        /// </summary>
        Task<CronogramaCorreoListaEnvio?> GetListaEnvioAsync(string codigo);

        /// <summary>Los feriados de ese mes (Configuración → Feriados): no cuentan como días hábiles.</summary>
        Task<HashSet<DateOnly>> GetFeriadosAsync(int anio, int mes);
    }
}
