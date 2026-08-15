using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Interfaces
{
    /// <summary>
    /// Aprobación de la solicitud de personal: primer paso del flujo. Manda UN correo con todas las
    /// vacantes al Gerente General y al gerente del área del solicitante, recibe la decisión de
    /// cada uno desde la pantalla «Aprobaciones» (con sesión iniciada) y, cuando la que decide es
    /// Gerencia General, notifica a GTH las vacantes aprobadas.
    ///
    /// Quién ve y quién decide qué lo resuelve <see cref="IAprobacionScopeResolver"/> desde la
    /// categoría de la ficha de trabajador, no desde el rol: el rol solo abre la pantalla.
    /// </summary>
    public interface IAprobacionGgService
    {
        /// <summary>
        /// Envía el correo de aprobación a los gerentes (destinatarios del tipo APROBACION_GG:
        /// Gerente General + gerente del área del solicitante). Devuelve true si el correo salió;
        /// false si no se pudo enviar (sin destinatarios configurados o error del proveedor) — la
        /// solicitud queda esperando un reenvío. No lanza: la solicitud ya está registrada y no
        /// debe caerse por el correo.
        /// </summary>
        Task<bool> EnviarSolicitudAGerencia(int solicitudId, int? userId);

        /// <summary>
        /// Reenvía el correo de aprobación desde el panel del solicitante (para cuando el primer
        /// envío falló o hubo que corregir los destinatarios). Mismo token: el enlace anterior
        /// sigue siendo válido.
        /// </summary>
        Task<AprobacionGgReenvioResultDto> Reenviar(int requerimientoId, int? userId);

        /// <summary>
        /// Pantalla «Aprobaciones» para este usuario: su nivel, las tarjetas de resumen calculadas
        /// contra SU casilla y las solicitudes que alcanza, en una sola petición.
        /// </summary>
        Task<AprobacionGgBandejaDto> GetBandeja(int? userId);

        /// <summary>
        /// Detalle de una aprobación para el modal (de decisión o de solo consulta). 403 si la
        /// solicitud está fuera del alcance del usuario.
        /// </summary>
        Task<AprobacionGgDetalleDto> GetDetalle(int aprobacionId, int? userId);

        /// <summary>
        /// Registra la decisión del usuario en la casilla de su nivel. Si quien decide es Gerencia
        /// General, además notifica a GTH las vacantes aprobadas (correo del tipo SOLICITUD +
        /// campanita); el visto bueno del gerente del área no dispara ningún correo.
        /// </summary>
        Task<AprobacionGgDecisionResultDto> RegistrarDecision(int aprobacionId, AprobacionGgDecisionDto dto, int? userId);
    }
}
