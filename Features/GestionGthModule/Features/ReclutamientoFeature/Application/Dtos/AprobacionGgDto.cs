namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Una de las dos casillas de decisión de la solicitud (gerente del área o Gerencia General),
    /// tal como se muestra en la lista y en el modal. Las dos tienen la misma forma para que la
    /// pantalla las pinte con el mismo componente.
    /// </summary>
    public class AprobacionNivelResumenDto
    {
        /// <summary>PENDIENTE / APROBADA / APROBADA_PARCIAL / RECHAZADA.</summary>
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>true cuando ese nivel ya decidió (deja de admitir cambios de ESE nivel).</summary>
        public bool Decidida { get; set; }

        /// <summary>Momento de la decisión en hora Perú (null si ese nivel sigue pendiente).</summary>
        public DateTime? DecididoEn { get; set; }

        /// <summary>
        /// Quién decidió. Null si sigue pendiente o si es una aprobación anterior a la pantalla
        /// (las decididas por el enlace del correo no dejaban usuario).
        /// </summary>
        public string? DecididoPor { get; set; }

        public string? Comentario { get; set; }

        public int VacantesAprobadas { get; set; }
        public int VacantesRechazadas { get; set; }
    }

    /// <summary>
    /// Detalle de una aprobación para el modal de «Aprobaciones»: cabecera de la solicitud, sus
    /// vacantes y las DOS casillas de decisión, en una sola petición.
    ///
    /// El modal se arma según <see cref="Nivel"/>: el usuario solo puede marcar la casilla que le
    /// corresponde y ve la otra como información. Si su nivel ya decidió (o no tiene ninguno),
    /// abre completo en modo lectura.
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

        // ── Las dos casillas ─────────────────────────────────────────────────
        /// <summary>Visto bueno del gerente del área. No condiciona el avance de la solicitud.</summary>
        public AprobacionNivelResumenDto GerenteArea { get; set; } = new();

        /// <summary>Decisión de Gerencia General: la obligatoria, la que manda las vacantes a GTH.</summary>
        public AprobacionNivelResumenDto GerenteGeneral { get; set; } = new();

        // ── Con qué poder entra el usuario que abrió el modal ────────────────
        /// <summary>GERENTE_GENERAL / GERENTE_AREA / NINGUNO (ver <c>AprobacionNivel</c>).</summary>
        public string Nivel { get; set; } = string.Empty;

        /// <summary>
        /// true si este usuario todavía puede registrar SU decisión sobre esta solicitud: tiene
        /// nivel y ese nivel sigue pendiente. false ⇒ el modal abre en lectura.
        /// </summary>
        public bool PuedeDecidir { get; set; }

        public List<AprobacionGgVacanteDto> Vacantes { get; set; } = new();

        /// <summary>
        /// A quién le llegarán los correos que dispara la decisión: el de GTH (tipo SOLICITUD) y el
        /// de TI (tipo TI_VACANTES), fusionados en una sola lista. Salen del mismo resolver que usa
        /// el envío, así que el aviso del modal no puede prometer algo distinto de lo que se manda.
        /// Solo se resuelve cuando quien abre el modal es el Gerente General y aún no ha decidido:
        /// es el único caso en el que esos correos van a salir.
        /// </summary>
        public SolicitudDestinatariosDto? Destinatarios { get; set; }
    }

    /// <summary>
    /// Pantalla «Aprobaciones» (Gestión GTH) en una sola petición: el alcance del usuario, las
    /// tarjetas de resumen y las solicitudes que puede ver — las pendientes de su decisión y el
    /// historial.
    /// </summary>
    public class AprobacionGgBandejaDto
    {
        /// <summary>GERENTE_GENERAL / GERENTE_AREA / NINGUNO (ver <c>AprobacionNivel</c>).</summary>
        public string Nivel { get; set; } = string.Empty;

        /// <summary>
        /// Área de la que el usuario es gerente (solo con nivel GERENTE_AREA), para poder explicar
        /// en pantalla por qué ve lo que ve.
        /// </summary>
        public string? AreaAlcance { get; set; }

        public AprobacionGgBandejaResumenDto Resumen { get; set; } = new();
        public List<AprobacionGgBandejaItemDto> Aprobaciones { get; set; } = new();
    }

    /// <summary>
    /// Contadores de las tarjetas. Se calculan SIEMPRE contra la casilla del usuario que consulta:
    /// "por aprobar" es lo que espera SU decisión, no lo que espera la del otro nivel. Todo en cero
    /// cuando el nivel es NINGUNO.
    /// </summary>
    public class AprobacionGgBandejaResumenDto
    {
        /// <summary>Solicitudes esperando la decisión de este usuario.</summary>
        public int Pendientes { get; set; }

        /// <summary>Vacantes que suman esas solicitudes pendientes (lo que está realmente en cola).</summary>
        public int VacantesPendientes { get; set; }

        /// <summary>Solicitudes que este usuario aprobó (total o parcialmente) — histórico.</summary>
        public int Aprobadas { get; set; }

        /// <summary>Solicitudes en las que este usuario rechazó todas las vacantes — histórico.</summary>
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

        public int TotalVacantes { get; set; }

        /// <summary>Visto bueno del gerente del área.</summary>
        public AprobacionNivelResumenDto GerenteArea { get; set; } = new();

        /// <summary>Decisión de Gerencia General (la que hace avanzar la solicitud).</summary>
        public AprobacionNivelResumenDto GerenteGeneral { get; set; } = new();

        /// <summary>
        /// true si esta fila espera la decisión del usuario que consulta. Es lo que decide el orden
        /// de la lista y el botón de la fila ("Revisar y aprobar" vs "Ver decisión").
        /// </summary>
        public bool EsperaMiDecision { get; set; }
    }

    /// <summary>Una vacante de la solicitud, con la decisión de cada nivel.</summary>
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

        /// <summary>Visto bueno del gerente del área: true / false / null = no opinó.</summary>
        public bool? AprobadoGerenteArea { get; set; }

        /// <summary>Decisión de Gerencia General: true = aprobada, false = rechazada, null = sin decidir.</summary>
        public bool? AprobadoGerenteGeneral { get; set; }
    }

    /// <summary>
    /// Decisión que envía un gerente desde la pantalla «Aprobaciones». El nivel con el que se
    /// registra NO viaja en el payload: lo resuelve el backend desde la categoría del usuario, para
    /// que nadie pueda pedir que su firma cuente como la del Gerente General.
    /// </summary>
    public class AprobacionGgDecisionDto
    {
        public List<VacanteDecisionGgDto> Decisiones { get; set; } = new();

        /// <summary>Comentario opcional (motivo del rechazo, condiciones, etc.).</summary>
        public string? Comentario { get; set; }
    }

    /// <summary>Decisión sobre una vacante concreta.</summary>
    public class VacanteDecisionGgDto
    {
        public int RequerimientoId { get; set; }
        public bool Aprobado { get; set; }
    }

    /// <summary>Resultado de registrar una decisión.</summary>
    public class AprobacionGgDecisionResultDto
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>Nivel con el que se registró (GERENTE_GENERAL / GERENTE_AREA).</summary>
        public string Nivel { get; set; } = string.Empty;

        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;
        public int Aprobados { get; set; }
        public int Rechazados { get; set; }
    }

    /// <summary>
    /// Contexto para armar el correo que va a los gerentes (y, tras la decisión del GG, el de GTH
    /// con las vacantes aprobadas). Lo resuelve el repositorio en un solo roundtrip.
    /// </summary>
    public class AprobacionGgEnvioContextoDto
    {
        public int SolicitudId { get; set; }

        /// <summary>Id de la aprobación: es lo que viaja en el enlace del correo.</summary>
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

        /// <summary>
        /// true si Gerencia General ya decidió. Es lo que cierra la solicitud: con esto no se
        /// reenvía el correo (el visto bueno del área pendiente no lo justifica, ya no cambia nada).
        /// </summary>
        public bool Decidida { get; set; }

        public List<AprobacionGgVacanteDto> Vacantes { get; set; } = new();
    }

    /// <summary>
    /// Contexto de la decisión ya registrada: lo que necesita el servicio para notificar a GTH
    /// (solo cuando decide el GG, y solo con las vacantes aprobadas) y para armar el mensaje de
    /// respuesta de la pantalla.
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

        /// <summary>
        /// Visto bueno que el gerente del área dejó registrado, si ya lo hizo. Va en el correo a GTH
        /// como contexto de la decisión del GG (null si el área nunca opinó).
        /// </summary>
        public string? GerenteAreaResumen { get; set; }

        /// <summary>Vacantes aprobadas en esta decisión: son las únicas que se le mandan a GTH.</summary>
        public List<AprobacionGgVacanteDto> Aprobadas { get; set; } = new();

        /// <summary>Vacantes rechazadas en esta decisión (se listan en el correo a GTH como contexto).</summary>
        public List<AprobacionGgVacanteDto> Rechazadas { get; set; } = new();
    }

    /// <summary>
    /// Resumen de la aprobación de un requerimiento, para la tarjeta "Aprobación" del modal de
    /// seguimiento del solicitante. Null en los requerimientos anteriores a esta funcionalidad (no
    /// pasaron por el paso de aprobación).
    /// </summary>
    public class AprobacionGgResumenDto
    {
        /// <summary>Estado de la decisión de Gerencia General sobre la solicitud completa.</summary>
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>Decisión del GG sobre ESTA vacante: true / false / null = sin decidir.</summary>
        public bool? Aprobado { get; set; }

        /// <summary>Estado del visto bueno del gerente del área sobre la solicitud completa.</summary>
        public string GerenteAreaEstadoCodigo { get; set; } = string.Empty;
        public string GerenteAreaEstadoNombre { get; set; } = string.Empty;

        /// <summary>Visto bueno del gerente del área sobre ESTA vacante: true / false / null = no opinó.</summary>
        public bool? AprobadoGerenteArea { get; set; }

        /// <summary>Momento del envío del correo a los gerentes en hora Perú (null si nunca se pudo enviar).</summary>
        public DateTime? EnviadoEn { get; set; }

        /// <summary>Momento de la decisión del GG en hora Perú (null si sigue pendiente).</summary>
        public DateTime? DecididoEn { get; set; }

        /// <summary>Momento del visto bueno del gerente del área en hora Perú.</summary>
        public DateTime? GerenteAreaDecididoEn { get; set; }

        /// <summary>Comentario del Gerente General.</summary>
        public string? Comentario { get; set; }

        /// <summary>Comentario del gerente del área.</summary>
        public string? GerenteAreaComentario { get; set; }
    }

    /// <summary>Resultado de reenviar el correo de aprobación a los gerentes.</summary>
    public class AprobacionGgReenvioResultDto
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>Destinatarios principales a los que se envió (para mostrarlos en el mensaje).</summary>
        public List<string> Destinatarios { get; set; } = new();
    }
}
