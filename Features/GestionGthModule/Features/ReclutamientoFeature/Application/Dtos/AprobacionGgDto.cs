namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Detalle de una aprobación para el modal de «Aprobaciones»: cabecera de la solicitud + todas
    /// sus vacantes, en una sola petición. Si ya fue decidida, <see cref="Decidida"/> es true y el
    /// modal se muestra en modo lectura con lo que quedó registrado (es también el historial).
    /// </summary>
    public class AprobacionGgDetalleDto
    {
        /// <summary>Id de la aprobación (el que viaja en el enlace del correo).</summary>
        public int AprobacionId { get; set; }

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

        /// <summary>true cuando el GG ya decidió: la pantalla no permite volver a decidir.</summary>
        public bool Decidida { get; set; }

        /// <summary>Momento de la decisión en hora Perú (null si aún está pendiente).</summary>
        public DateTime? DecididoEn { get; set; }

        /// <summary>
        /// Quién registró la decisión. Null si sigue pendiente o si es una aprobación anterior a
        /// la pantalla (las decididas por el enlace del correo no dejaban usuario).
        /// </summary>
        public string? DecididoPor { get; set; }

        public string? Comentario { get; set; }

        public List<AprobacionGgVacanteDto> Vacantes { get; set; } = new();
    }

    /// <summary>
    /// Pantalla «Aprobaciones» (Gestión GTH) en una sola petición: tarjetas de resumen + la lista
    /// completa de solicitudes que pasaron por Gerencia General — las pendientes de decidir y el
    /// historial de las ya decididas.
    /// </summary>
    public class AprobacionGgBandejaDto
    {
        public AprobacionGgBandejaResumenDto Resumen { get; set; } = new();
        public List<AprobacionGgBandejaItemDto> Aprobaciones { get; set; } = new();
    }

    /// <summary>Contadores de las tarjetas de la pantalla «Aprobaciones».</summary>
    public class AprobacionGgBandejaResumenDto
    {
        /// <summary>Solicitudes esperando la decisión de Gerencia General.</summary>
        public int Pendientes { get; set; }

        /// <summary>Vacantes que suman esas solicitudes pendientes (lo que está realmente en cola).</summary>
        public int VacantesPendientes { get; set; }

        /// <summary>Solicitudes aprobadas (total o parcialmente) — histórico.</summary>
        public int Aprobadas { get; set; }

        /// <summary>Solicitudes en las que se rechazaron todas las vacantes — histórico.</summary>
        public int Rechazadas { get; set; }
    }

    /// <summary>Una solicitud en la lista de «Aprobaciones» (una fila = una solicitud de personal).</summary>
    public class AprobacionGgBandejaItemDto
    {
        public int AprobacionId { get; set; }

        /// <summary>Códigos de las vacantes de la solicitud, separados por ", " (para buscar y mostrar).</summary>
        public string Codigos { get; set; } = string.Empty;

        public string? Area { get; set; }
        public string? SolicitanteNombre { get; set; }
        public string? Justificacion { get; set; }

        /// <summary>Fecha de registro de la solicitud en hora Perú (UTC-5).</summary>
        public DateTime Enviado { get; set; }

        /// <summary>PENDIENTE / APROBADA / APROBADA_PARCIAL / RECHAZADA.</summary>
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>true cuando ya se decidió: la fila es historial y el modal abre en lectura.</summary>
        public bool Decidida { get; set; }

        public DateTime? DecididoEn { get; set; }
        public string? DecididoPor { get; set; }

        public int TotalVacantes { get; set; }
        public int VacantesAprobadas { get; set; }
        public int VacantesRechazadas { get; set; }
    }

    /// <summary>Una vacante de la solicitud como la ve (y decide) Gerencia General.</summary>
    public class AprobacionGgVacanteDto
    {
        public int RequerimientoId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Puesto { get; set; } = string.Empty;

        /// <summary>Tipo de requerimiento (Nuevo / Reemplazo).</summary>
        public string TipoRequerimiento { get; set; } = string.Empty;

        /// <summary>
        /// Trabajador al que reemplaza la vacante: es el dato que le da sentido a un Reemplazo a la
        /// hora de aprobarlo. Null en las vacantes nuevas y en las anteriores a este dato.
        /// </summary>
        public string? TrabajadorReemplazado { get; set; }

        public string? ProyectoObra { get; set; }
        public DateOnly FechaRequeridaIngreso { get; set; }

        /// <summary>Decisión registrada: true = aprobada, false = rechazada, null = sin decidir.</summary>
        public bool? Aprobado { get; set; }
    }

    /// <summary>Decisión que envía Gerencia General desde la pantalla «Aprobaciones».</summary>
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

        /// <summary>Id de la aprobación: es lo que viaja en el enlace del correo a Gerencia.</summary>
        public int AprobacionId { get; set; }

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
    /// (solo las vacantes aprobadas) y para armar el mensaje de respuesta de la pantalla.
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
