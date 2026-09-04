namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Interfaces
{
    /// <summary>
    /// El correo que avisa el resultado de un EMO (evento <c>RESULTADO</c> de la Configuración
    /// de EMOs) al médico ocupacional, a GTH y a la jefatura del trabajador — o al solicitante de
    /// la vacante, si la ficha todavía es de pre-ingreso.
    ///
    /// Se dispara al registrar el examen (lo hace la clínica desde Clínica → Agenda, y también
    /// SSOMA desde EMOs). Solo sale con un veredicto cerrado: Apto, Apto con Restricciones o No
    /// Apto. "Observado" no envía nada — significa que falta la interconsulta y todavía no hay
    /// resultado que comunicar.
    /// </summary>
    public interface IEmoResultadoNotificacionService
    {
        /// <summary>
        /// Envía el correo del EMO indicado. Es best-effort: cualquier error se registra en el log
        /// y no se propaga — un correo que no sale no puede tumbar el registro de un examen médico
        /// que ya está guardado.
        /// </summary>
        Task NotificarAsync(int emoId);
    }
}
