using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Interfaces
{
    /// <summary>
    /// Los recordatorios del plazo de rendición (RG-33 y RG-34): el cron llama todos los días y acá
    /// se decide si hoy toca alguno. El envío manual de la Configuración simula un día cualquiera con
    /// el mismo cálculo.
    /// </summary>
    public interface IRecordatorioRendicionService
    {
        /// <summary>
        /// Corre el recordatorio del día. Devuelve qué se hizo aunque no haya sido nada — es lo
        /// único que queda en el log del cron.
        /// </summary>
        Task<RecordatorioRendicionResultDto> EjecutarAsync();

        /// <summary>
        /// Lo que saldría el recordatorio <paramref name="eventoCodigo"/> si el cron corriera el día
        /// <paramref name="fecha"/>, sin enviar nada.
        /// </summary>
        Task<RecordatorioSimulacionDto> SimularAsync(string eventoCodigo, DateOnly fecha);

        /// <summary>Manda lo que saldría ese día. Si ese día no sale nada, no manda nada.</summary>
        Task<RecordatorioEnvioManualDto> EnviarManualAsync(string eventoCodigo, DateOnly fecha);
    }
}
