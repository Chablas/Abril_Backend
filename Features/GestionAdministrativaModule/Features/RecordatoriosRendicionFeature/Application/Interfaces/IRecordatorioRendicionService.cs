using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Interfaces
{
    /// <summary>
    /// Los recordatorios del plazo de rendición (RG-33 y RG-34). Un solo método porque hay un solo
    /// endpoint: el cron llama todos los días y acá se decide si hoy toca alguno.
    /// </summary>
    public interface IRecordatorioRendicionService
    {
        /// <summary>
        /// Corre el recordatorio del día. Devuelve qué se hizo aunque no haya sido nada — es lo
        /// único que queda en el log del cron.
        /// </summary>
        Task<RecordatorioRendicionResultDto> EjecutarAsync();
    }
}
