using Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos;

namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Interfaces
{
    /// <summary>
    /// Persistencia de la aprobación de Gerencia General de una solicitud de personal
    /// (<c>gth_aprobacion_gg</c> + su detalle por vacante).
    /// </summary>
    public interface IAprobacionGgRepository
    {
        /// <summary>
        /// Crea la aprobación PENDIENTE de la solicitud (con una fila de detalle por vacante) y
        /// devuelve el contexto para armar el correo. Si ya existía una aprobación vigente devuelve
        /// esa (idempotente): el token no se regenera para que el enlace ya enviado siga sirviendo.
        /// </summary>
        Task<AprobacionGgEnvioContextoDto> PrepararEnvio(int solicitudId, string nuevoToken, int? userId);

        /// <summary>
        /// Contexto del correo de una aprobación existente, con scope al solicitante dueño de la
        /// solicitud (para el reenvío desde su panel). Null si no existe o no es suya.
        /// </summary>
        Task<AprobacionGgEnvioContextoDto?> GetEnvioContextoByRequerimiento(int requerimientoId, int userId);

        /// <summary>Registra los destinatarios y el momento del envío del correo (o del reenvío).</summary>
        Task RegistrarEnvio(int aprobacionId, List<string> principales, List<string> copias, bool esReenvio, int? userId);

        /// <summary>Datos de la página pública por token. Null si el token no corresponde a una aprobación vigente.</summary>
        Task<AprobacionGgPublicoDto?> GetPublicoByToken(string token);

        /// <summary>
        /// Registra la decisión del GG por token: guarda el detalle, mueve cada requerimiento
        /// (aprobado → VALIDACION_GTH, rechazado → RECHAZADO_GG) y cierra la aprobación.
        /// Devuelve el contexto con las vacantes aprobadas para el correo a GTH.
        /// </summary>
        Task<AprobacionGgDecisionContextoDto> RegistrarDecision(string token, AprobacionGgDecisionDto dto);

        /// <summary>
        /// Resumen de la aprobación de un requerimiento para la tarjeta del seguimiento.
        /// Null en los requerimientos que no pasaron por el paso del GG.
        /// </summary>
        Task<AprobacionGgResumenDto?> GetResumenByRequerimiento(int requerimientoId);
    }
}
