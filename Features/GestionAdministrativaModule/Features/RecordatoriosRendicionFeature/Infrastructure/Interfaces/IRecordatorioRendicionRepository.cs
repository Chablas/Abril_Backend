using Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.RecordatoriosRendicion.Infrastructure.Interfaces
{
    /// <summary>
    /// Las dos lecturas del recordatorio del plazo de rendición, separadas a propósito: la ventana
    /// es barata y se pregunta todos los días; los pendientes solo se buscan los dos días del mes
    /// en que hay algo que mandar.
    /// </summary>
    public interface IRecordatorioRendicionRepository
    {
        /// <summary>
        /// Qué mes se está rindiendo hoy, hasta cuándo, y si hoy toca alguno de los dos
        /// recordatorios. El plazo sale de <c>ga_rendicion_config</c> y los días hábiles se cuentan
        /// contra los feriados de Configuración → Feriados.
        /// </summary>
        Task<RecordatorioVentanaDto> GetVentanaAsync();

        /// <summary>
        /// Trabajadores con salidas de ese periodo aptas para rendir y todavía sin rendir, con el
        /// detalle de cada salida. Solo entran los que tienen <c>email_corporativo</c>: sin correo
        /// no hay a quién recordarle nada.
        /// </summary>
        Task<List<RecordatorioTrabajadorDto>> GetPendientesAsync(DateOnly desde, DateOnly hasta);
    }
}
