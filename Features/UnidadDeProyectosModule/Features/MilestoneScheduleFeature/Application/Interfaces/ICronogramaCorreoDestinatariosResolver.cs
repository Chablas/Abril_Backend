using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces
{
    /// <summary>
    /// A quién le llega cada correo del Cronograma de Hitos, según Cronograma de Hitos →
    /// Configuración. Lo usan el aviso de versión con cambios (MilestoneScheduleHistoryController)
    /// y los dos recordatorios (ReminderService).
    /// </summary>
    public interface ICronogramaCorreoDestinatariosResolver
    {
        /// <summary>
        /// La lista del correo <paramref name="codigo"/> (<c>CronogramaHitosCorreos</c>), para armar
        /// cada envío con <see cref="CronogramaCorreoListaEnvio.Armar"/>. Nunca falla: si la
        /// configuración no se puede leer, le llega solo al destinatario del sistema (y se registra).
        /// </summary>
        Task<CronogramaCorreoListaEnvio> ObtenerAsync(string codigo);
    }
}
