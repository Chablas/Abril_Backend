namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Aprobación de Gerencia General de una solicitud de personal
    /// (tabla <c>gth_aprobacion_gg</c>): 1:1 con <see cref="GthSolicitud"/> entre las vigentes.
    ///
    /// Es el primer paso del flujo: al registrar la solicitud se le manda al Gerente General UN
    /// SOLO correo con todas las vacantes y un enlace a la pantalla «Aprobaciones» del módulo de
    /// Gestión GTH, donde aprueba todas, algunas, o rechaza todas. Solo las aprobadas pasan a GTH.
    ///
    /// La decisión se toma dentro de la aplicación (con sesión iniciada), así que
    /// <see cref="DecididoUserId"/> guarda quién la tomó. <see cref="CorreoEnvio"/> /
    /// <see cref="CorreoCopia"/> siguen siendo el snapshot de a quiénes se les avisó.
    /// </summary>
    public class GthAprobacionGg
    {
        public int GthAprobacionGgId { get; set; }

        /// <summary>FK a la solicitud dueña de la aprobación (1:1 entre las vigentes).</summary>
        public int GthSolicitudId { get; set; }

        /// <summary>
        /// Identificador aleatorio de la aprobación. Nació como token de acceso a la página
        /// pública de decisión (sin login); esa página ya no existe — hoy el gerente entra a
        /// «Aprobaciones» con su sesión. Se sigue generando porque la columna es NOT NULL y
        /// tiene índice único entre las vigentes, pero NO otorga acceso a nada.
        /// </summary>
        public string Token { get; set; } = null!;

        /// <summary>FK a <see cref="GthAprobacionGgEstado"/>: PENDIENTE / APROBADA / APROBADA_PARCIAL / RECHAZADA.</summary>
        public int GthAprobacionGgEstadoId { get; set; }

        /// <summary>Destinatarios principales (Para) a los que se envió el correo, separados por "; ".</summary>
        public string? CorreoEnvio { get; set; }

        /// <summary>Destinatarios en copia (CC) a los que se envió el correo, separados por "; ".</summary>
        public string? CorreoCopia { get; set; }

        public DateTimeOffset? EnviadoDateTime { get; set; }

        /// <summary>Último reenvío del correo ("Reenviar a Gerencia General"). El token no cambia.</summary>
        public DateTimeOffset? ReenviadoDateTime { get; set; }

        public DateTimeOffset? DecididoDateTime { get; set; }

        /// <summary>
        /// Usuario que registró la decisión desde «Aprobaciones». Es la traza de quién aprobó o
        /// rechazó; va aparte de <c>updated_user_id</c> para que un update posterior no la pise.
        /// Null en las aprobaciones anteriores a la pantalla (se decidían por enlace, sin sesión).
        /// </summary>
        public int? DecididoUserId { get; set; }

        /// <summary>Comentario opcional que el GG escribe al confirmar su decisión.</summary>
        public string? Comentario { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; } = true;
        public bool State { get; set; } = true;
    }
}
