using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces
{
    /// <summary>
    /// Aprobación de Gerencia General: primer paso del flujo de solicitud de personal. Manda UN
    /// correo con todas las vacantes de la solicitud, recibe la decisión desde la pantalla
    /// «Aprobaciones» (con sesión iniciada) y, solo entonces, notifica a GTH las aprobadas.
    /// </summary>
    public interface IAprobacionGgService
    {
        /// <summary>
        /// Envía el correo de aprobación al Gerente General (destinatarios del tipo APROBACION_GG).
        /// Devuelve true si el correo salió; false si no se pudo enviar (sin destinatarios
        /// configurados o error del proveedor) — la solicitud queda esperando un reenvío.
        /// No lanza: la solicitud ya está registrada y no debe caerse por el correo.
        /// </summary>
        Task<bool> EnviarSolicitudAGerencia(int solicitudId, int? userId);

        /// <summary>
        /// Reenvía el correo de aprobación al GG desde el panel del solicitante (para cuando el
        /// primer envío falló o hubo que corregir los destinatarios). Mismo token: el enlace
        /// anterior sigue siendo válido.
        /// </summary>
        Task<AprobacionGgReenvioResultDto> Reenviar(int requerimientoId, int? userId);

        /// <summary>
        /// Pantalla «Aprobaciones»: tarjetas de resumen + las solicitudes pendientes de decidir y
        /// el historial de las ya decididas, en una sola petición.
        /// </summary>
        Task<AprobacionGgBandejaDto> GetBandeja();

        /// <summary>Detalle de una aprobación para el modal de decisión (o de consulta si ya se decidió).</summary>
        Task<AprobacionGgDetalleDto> GetDetalle(int aprobacionId);

        /// <summary>
        /// Registra la decisión del GG (aprobar todas, algunas o rechazar todas) y notifica a GTH
        /// las vacantes aprobadas (correo del tipo SOLICITUD + campanita).
        /// </summary>
        Task<AprobacionGgDecisionResultDto> RegistrarDecision(int aprobacionId, AprobacionGgDecisionDto dto, int? userId);
    }
}
