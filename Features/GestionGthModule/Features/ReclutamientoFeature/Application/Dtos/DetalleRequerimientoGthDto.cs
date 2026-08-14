namespace Abril_Backend.Features.GestionGthModule.Features.ReclutamientoFeature.Application.Dtos
{
    /// <summary>
    /// Detalle de un requerimiento para la vista de GTH (modal del ojo de la bandeja):
    /// cabecera + asignación interna actual + catálogos de los desplegables (con cupos por
    /// razón social) + canales de publicación, todo en una sola petición.
    /// </summary>
    public class DetalleRequerimientoGthDto
    {
        public int RequerimientoId { get; set; }

        /// <summary>Código REQ-AAAA-NNNN.</summary>
        public string Codigo { get; set; } = string.Empty;

        public string Puesto { get; set; } = string.Empty;

        /// <summary>Área solicitante (snapshot al registrar).</summary>
        public string? Area { get; set; }

        /// <summary>Proyecto/obra destino de la vacante.</summary>
        public string? ProyectoObra { get; set; }

        /// <summary>Tipo de requerimiento (Nuevo / Reemplazo).</summary>
        public string TipoRequerimiento { get; set; } = string.Empty;

        /// <summary>
        /// Trabajador al que reemplaza la vacante. Solo lo traen los requerimientos de tipo
        /// Reemplazo registrados desde que se pide ese dato; null en el resto.
        /// </summary>
        public string? TrabajadorReemplazado { get; set; }

        /// <summary>Vacantes de este requerimiento (cada vacante genera un requerimiento → 1).</summary>
        public int Vacantes { get; set; } = 1;

        public DateOnly FechaRequeridaIngreso { get; set; }

        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;

        /// <summary>Asignación interna de GTH actual del requerimiento.</summary>
        public AsignacionGthDto Asignacion { get; set; } = new();

        // ── Catálogos de los desplegables ─────────────────────────────────────
        /// <summary>Miembros GTH que pueden ser responsables del proceso.</summary>
        public List<OpcionDto> Responsables { get; set; } = new();

        public List<TipoProcesoOpcionDto> TiposProceso { get; set; } = new();

        public List<OpcionDto> Prioridades { get; set; } = new();

        /// <summary>Razones sociales activas (contributor.operativo = true) con sus cupos.</summary>
        public List<RazonSocialOpcionDto> RazonesSociales { get; set; } = new();

        /// <summary>Canales de publicación con su estado de publicación para este requerimiento.</summary>
        public List<CanalPublicacionDto> Canales { get; set; } = new();

        /// <summary>Lugares donde se puede citar al candidato (desplegable de programación de entrevistas).</summary>
        public List<OpcionDto> LugaresEntrevista { get; set; } = new();

        /// <summary>
        /// Candidatos APROBADOS por el solicitante (solo relevante cuando el requerimiento ya está
        /// en LONG_LIST_APROBADA). Alimentan la vista de GTH "Long list aprobada": formulario del
        /// postulante y control del Multitest, y luego la programación de entrevistas. Vacío en
        /// fases anteriores.
        /// </summary>
        public List<CandidatoAprobadoDto> CandidatosAprobados { get; set; } = new();

        /// <summary>
        /// Candidatos RECHAZADOS a lo largo del proceso, con la etapa del rechazo, incluidos los de
        /// long lists anteriores. Alimenta la sección «Historial de candidatos rechazados»: cuando
        /// el solicitante rechaza a todos y el requerimiento vuelve a LONG_LIST, es lo que GTH mira
        /// para no volver a presentar a los mismos.
        /// </summary>
        public List<CandidatoRechazadoDto> CandidatosRechazados { get; set; } = new();

        /// <summary>
        /// Quién obtuvo el puesto: el candidato que el solicitante aprobó en la decisión final.
        /// Null mientras el proceso no se haya cerrado con un seleccionado.
        /// </summary>
        public SeleccionadoDto? Seleccionado { get; set; }
    }

    /// <summary>Candidato aprobado por el solicitante, tal como lo ve GTH en la fase "Long list aprobada".</summary>
    public class CandidatoAprobadoDto
    {
        public int CandidatoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Puesto { get; set; }

        /// <summary>Estado del formulario de información del postulante de este candidato (null si GTH aún no lo envió).</summary>
        public CandidatoFormularioResumenDto? Formulario { get; set; }

        /// <summary>true si GTH ya marcó el check informativo del Multitest de este candidato.</summary>
        public bool MultitestRealizado { get; set; }

        /// <summary>
        /// Correo del postulante al que se enviará la invitación a la entrevista: el que declaró en
        /// el formulario y, si no lo declaró, aquel al que GTH le envió el enlace. Null si aún no
        /// hay formulario.
        /// </summary>
        public string? CorreoContacto { get; set; }

        /// <summary>Entrevista programada de este candidato (null si aún no se programó).</summary>
        public EntrevistaResumenDto? Entrevista { get; set; }

        /// <summary>
        /// Evaluación de la entrevista (comentarios del informe y resultado). Null mientras GTH
        /// no registre nada ni envíe el correo de agradecimiento.
        /// </summary>
        public EvaluacionResumenDto? Evaluacion { get; set; }
    }

    /// <summary>Asignación interna de GTH de un requerimiento (todas opcionales/null = sin asignar).</summary>
    public class AsignacionGthDto
    {
        /// <summary>Id de gth_responsable_proceso (responsable del proceso).</summary>
        public int? ResponsableId { get; set; }

        /// <summary>Id de gth_tipo_proceso (tipo de proceso y SLA).</summary>
        public int? TipoProcesoId { get; set; }

        /// <summary>Id de gth_prioridad (prioridad interna).</summary>
        public int? PrioridadId { get; set; }

        /// <summary>Id de contributor (razón social activa).</summary>
        public int? ContributorId { get; set; }
    }

    /// <summary>Opción del desplegable "Tipo de proceso y SLA".</summary>
    public class TipoProcesoOpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int SlaDias { get; set; }
        public string? Descripcion { get; set; }
    }

    /// <summary>Opción del desplegable "Razón social activa", con sus cupos disponibles.</summary>
    public class RazonSocialOpcionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>
        /// Cupos disponibles = tope (20) − trabajadores vigentes de la razón social en la base
        /// maestra que son de Staff, Oficina Central o Personal Externo (el personal de Obra y
        /// los practicantes no consumen cupo). Nunca negativo (se muestra 0).
        /// </summary>
        public int CuposDisponibles { get; set; }
    }

    /// <summary>
    /// Canal de publicación de vacantes y su estado para el requerimiento consultado. No hay
    /// integración con las APIs de los portales: marcar el canal solo deja registro de dónde se
    /// publicó, la publicación siempre la hace GTH manualmente.
    /// </summary>
    public class CanalPublicacionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        /// <summary>true = la vacante ya está registrada como publicada en este canal.</summary>
        public bool Publicado { get; set; }
    }

    /// <summary>Body del PATCH que guarda la asignación interna de GTH (reemplaza los 4 campos).</summary>
    public class AsignacionGthUpdateDto
    {
        public int? ResponsableId { get; set; }
        public int? TipoProcesoId { get; set; }
        public int? PrioridadId { get; set; }
        public int? ContributorId { get; set; }
    }

    /// <summary>Body del PUT que registra los canales donde se publicó la vacante.</summary>
    public class PublicacionesUpdateDto
    {
        public List<int> CanalIds { get; set; } = new();
    }

    /// <summary>
    /// Estado resultante de una transición del pipeline (respuesta de registrar la publicación
    /// y de iniciar la revisión de CV): el frontend actualiza el badge y las secciones del
    /// modal sin volver a pedir el detalle.
    /// </summary>
    public class EstadoRequerimientoResultDto
    {
        public string EstadoCodigo { get; set; } = string.Empty;
        public string EstadoNombre { get; set; } = string.Empty;
    }
}
