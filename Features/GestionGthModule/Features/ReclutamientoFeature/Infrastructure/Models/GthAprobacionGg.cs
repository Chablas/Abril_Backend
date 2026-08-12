namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Infrastructure.Models
{
    /// <summary>
    /// Aprobación de Gerencia General de una solicitud de personal
    /// (tabla <c>gth_aprobacion_gg</c>): 1:1 con <see cref="GthSolicitud"/> entre las vigentes.
    ///
    /// Es el primer paso del flujo: al registrar la solicitud se le manda al Gerente General UN
    /// SOLO correo con todas las vacantes y un enlace con <see cref="Token"/> a una página pública
    /// (sin login) donde aprueba todas, algunas, o rechaza todas. Solo las aprobadas pasan a GTH.
    ///
    /// El GG no inicia sesión, así que la única traza de quién pudo decidir es el snapshot de los
    /// correos a los que se envió el enlace (<see cref="CorreoEnvio"/> / <see cref="CorreoCopia"/>).
    /// </summary>
    public class GthAprobacionGg
    {
        public int GthAprobacionGgId { get; set; }

        /// <summary>FK a la solicitud dueña de la aprobación (1:1 entre las vigentes).</summary>
        public int GthSolicitudId { get; set; }

        /// <summary>Token de acceso público a la página de decisión (va en el enlace del correo).</summary>
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
