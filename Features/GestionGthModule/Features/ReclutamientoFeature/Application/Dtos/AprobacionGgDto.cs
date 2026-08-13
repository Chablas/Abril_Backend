namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Página pública de decisión de Gerencia General (acceso por token, sin login): cabecera de la
    /// solicitud + todas sus vacantes, en una sola petición. Si la aprobación ya fue decidida,
    /// <see cref="Decidida"/> es true y la página se muestra en modo lectura con lo que se registró.
    /// </summary>
    public class AprobacionGgPublicoDto
    {
        /// <summary>Área solicitante (snapshot al registrar la solicitud).</summary>
        public string? Area { get; set; }

        /// <summary>Nombre del solicitante que registró la solicitud.</summary>
        public string? SolicitanteNombre { get; set; }

        public string? Justificacion { get; set; }

        /// <summary>Sustento adjunto de la solicitud (link de SharePoint), si hay.</summary>
        public string? SustentoNombre { get; set; }
        public string? SustentoUrl { get; set; }

        /// <summary>Fecha de registro de la solicitud en hora Perú (UTC-5).</summary>
        public DateTime Enviado { get; set; }

        // ── Estado de la aprobación ──────────────────────────────────────────
        /// <summary>PENDIENTE / APROBADA / APROBADA_PARCIAL / RECHAZADA.</summary>
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>true cuando el GG ya decidió: la página no permite volver a decidir.</summary>
        public bool Decidida { get; set; }

        /// <summary>Momento de la decisión en hora Perú (null si aún está pendiente).</summary>
        public DateTime? DecididoEn { get; set; }

        public string? Comentario { get; set; }

        public List<AprobacionGgVacanteDto> Vacantes { get; set; } = new();
    }

    /// <summary>Una vacante de la solicitud como la ve (y decide) Gerencia General.</summary>
    public class AprobacionGgVacanteDto
    {
        public int RequerimientoId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;

        /// <summary>Tipo de requerimiento (Nuevo / Reemplazo).</summary>
        public string TipoRequerimiento { get; set; } = string.Empty;

        public string? ProyectoObra { get; set; }
        public DateOnly FechaRequeridaIngreso { get; set; }

        /// <summary>Decisión registrada: true = aprobada, false = rechazada, null = sin decidir.</summary>
        public bool? Aprobado { get; set; }
    }

    /// <summary>Decisión que envía Gerencia General desde la página pública.</summary>
    public class AprobacionGgDecisionDto
    {
        public List<VacanteDecisionGgDto> Decisiones { get; set; } = new();

        /// <summary>Comentario opcional (motivo del rechazo, condiciones, etc.).</summary>
        public string? Comentario { get; set; }
    }

    /// <summary>Decisión de Gerencia General sobre una vacante concreta.</summary>
    public class VacanteDecisionGgDto
    {
        public int RequerimientoId { get; set; }
        public bool Aprobado { get; set; }
    }

    /// <summary>Resultado de registrar la decisión de Gerencia General.</summary>
    public class AprobacionGgDecisionResultDto
    {
        public string Message { get; set; } = string.Empty;
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;
        public int Aprobados { get; set; }
        public int Rechazados { get; set; }
    }

    /// <summary>
    /// Contexto para armar el correo de Gerencia General (y, tras la decisión, el de GTH con las
    /// vacantes aprobadas). Lo resuelve el repositorio en un solo roundtrip.
    /// </summary>
    public class AprobacionGgEnvioContextoDto
    {
        public int SolicitudId { get; set; }
        public int AprobacionId { get; set; }
        public string Token { get; set; } = string.Empty;

        public string? Area { get; set; }

        /// <summary>
        /// <c>area_scope</c> del solicitante (snapshot de la solicitud). Con él se resuelve al
        /// gerente del área, que recibe el correo junto al Gerente General.
        /// </summary>
        public int? AreaScopeId { get; set; }

        public string? SolicitanteNombre { get; set; }
        public string? Justificacion { get; set; }
        public string? SustentoNombre { get; set; }
        public string? SustentoUrl { get; set; }

        /// <summary>true si la aprobación ya fue decidida (no se reenvía el correo en ese caso).</summary>
        public bool Decidida { get; set; }

        public List<AprobacionGgVacanteDto> Vacantes { get; set; } = new();
    }

    /// <summary>
    /// Contexto de la decisión ya registrada: lo que necesita el servicio para notificar a GTH
    /// (solo las vacantes aprobadas) y para armar el mensaje de respuesta de la página pública.
    /// </summary>
    public class AprobacionGgDecisionContextoDto
    {
        public AprobacionGgDecisionResultDto Resultado { get; set; } = new();

        public int SolicitudId { get; set; }
        public string? Area { get; set; }
        public string? SolicitanteNombre { get; set; }
        public string? Justificacion { get; set; }
        public string? SustentoNombre { get; set; }
        public string? SustentoUrl { get; set; }
        public string? Comentario { get; set; }

        /// <summary>Vacantes que el GG aprobó: son las únicas que se le mandan a GTH.</summary>
        public List<AprobacionGgVacanteDto> Aprobadas { get; set; } = new();

        /// <summary>Vacantes que el GG rechazó (se listan en el correo a GTH como contexto).</summary>
        public List<AprobacionGgVacanteDto> Rechazadas { get; set; } = new();
    }

    /// <summary>
    /// Resumen de la aprobación de Gerencia General de un requerimiento, para la tarjeta
    /// "Aprobación GG" del modal de seguimiento. Null en los requerimientos anteriores a esta
    /// funcionalidad (no pasaron por el paso del GG).
    /// </summary>
    public class AprobacionGgResumenDto
    {
        /// <summary>PENDIENTE / APROBADA / APROBADA_PARCIAL / RECHAZADA (estado de la solicitud completa).</summary>
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>Decisión sobre ESTA vacante: true = aprobada, false = rechazada, null = sin decidir.</summary>
        public bool? Aprobado { get; set; }

        /// <summary>Momento del envío del correo al GG en hora Perú (null si nunca se pudo enviar).</summary>
        public DateTime? EnviadoEn { get; set; }

        /// <summary>Momento de la decisión en hora Perú (null si sigue pendiente).</summary>
        public DateTime? DecididoEn { get; set; }

        public string? Comentario { get; set; }
    }

    /// <summary>Resultado de reenviar el correo de aprobación a Gerencia General.</summary>
    public class AprobacionGgReenvioResultDto
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>Destinatarios principales a los que se envió (para mostrarlos en el mensaje).</summary>
        public List<string> Destinatarios { get; set; } = new();
    }
}
