using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces
{
    /// <summary>
    /// Cronograma de Hitos → Configuración: los correos y recordatorios del cronograma y sus
    /// destinatarios. Cada operación es una acción de la pantalla (guarda al tocar el control).
    /// </summary>
    public interface ICronogramaConfiguracionService
    {
        Task<CronogramaConfiguracionDto> GetAsync();

        /// <summary>Interruptor del correo: apagado, no se envía a nadie.</summary>
        Task SetCorreoActiveAsync(string codigo, bool active, int userId);

        /// <summary>Interruptor del destinatario que pone el sistema (el residente).</summary>
        Task SetPrincipalActiveAsync(string codigo, bool active, int userId);

        /// <summary>Agrega un destinatario y devuelve el correo con su lista actualizada.</summary>
        Task<CronogramaCorreoDto> CrearDestinatarioAsync(string codigo, CronogramaCorreoDestinatarioInputDto dto, int userId);

        /// <summary>Cambia a quién apunta un destinatario o cómo lo recibe.</summary>
        Task<CronogramaCorreoDto> ActualizarDestinatarioAsync(int id, CronogramaCorreoDestinatarioInputDto dto, int userId);

        Task SetDestinatarioActiveAsync(int id, bool active, int userId);

        /// <summary>Lo da de baja (se conserva la fila) y devuelve el correo con su lista actualizada.</summary>
        Task<CronogramaCorreoDto> EliminarDestinatarioAsync(int id, int userId);
    }
}
