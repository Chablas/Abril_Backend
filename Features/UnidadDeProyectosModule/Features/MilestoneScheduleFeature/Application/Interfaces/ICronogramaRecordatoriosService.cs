using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces
{
    /// <summary>
    /// Los dos recordatorios del Cronograma de Hitos (cronograma pendiente y resumen del mes): qué
    /// día sale cada uno, a quién y con qué. El cron y el envío manual de la Configuración pasan por
    /// el mismo cálculo, así que simular un día dice exactamente lo que el cron mandaría ese día.
    /// </summary>
    public interface ICronogramaRecordatoriosService
    {
        /// <summary>El cron (ReminderService), todos los días: manda los que salen <paramref name="hoy"/>.</summary>
        Task EnviarDelDiaAsync(DateOnly hoy);

        /// <summary>Lo que saldría el recordatorio <paramref name="codigo"/> el día <paramref name="fecha"/>, sin enviar nada.</summary>
        Task<CronogramaRecordatorioSimulacionDto> SimularAsync(string codigo, DateOnly fecha);

        /// <summary>Manda lo que saldría ese día. Si ese día no sale nada, no manda nada.</summary>
        Task<CronogramaRecordatorioEnvioDto> EnviarManualAsync(string codigo, DateOnly fecha);
    }
}
