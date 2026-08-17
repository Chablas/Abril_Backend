namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Application.Dtos
{
    // ── Genéricos ────────────────────────────────────────────────────────────
    public class CatalogoDto
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = null!;
    }

    public class ProyectoFiltroDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
    }

    /// <summary>
    /// Tema del desplegable "Tema de la reunión", con el área/gerencia de su convocatoria recurrente
    /// (si tiene) para poder ocultarlo cuando no aplica al ámbito elegido — ej. "Reunión de Jefaturas
    /// de Proyectos" (AreaScopeId = Gerencia de Proyectos) no debe aparecer al agendar una reunión de
    /// un proyecto puntual. AreaScopeId null = sin área asociada, aplica a cualquier ámbito.
    /// </summary>
    public class ReunionTemaOpcionDto
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = null!;
        public int? AreaScopeId { get; set; }
    }

    /// <summary>Trabajador de Abril (workers con email_corporativo @abril.pe) para los desplegables.</summary>
    public class TrabajadorAbrilDto
    {
        public int WorkerId { get; set; }
        public string FullName { get; set; } = null!;
        /// <summary>Nombre del puesto del trabajador (catálogo <c>puesto</c>).</summary>
        public string? Cargo { get; set; }
    }

    public class PagedResultDto<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalRecords { get; set; }
        public int TotalPages { get; set; }
        public List<T> Data { get; set; } = new();
    }

    // ── Listado ──────────────────────────────────────────────────────────────
    public class ReunionFiltroRequest
    {
        public int? ProjectId { get; set; }
        /// <summary>Nodo del árbol area_scope; incluye descendientes (ver AreaScopeTree.ResolveDescendantsAsync).</summary>
        public int? AreaScopeId { get; set; }
        public int? ReunionEstadoId { get; set; }
        public DateOnly? Desde { get; set; }
        public DateOnly? Hasta { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class ReunionListItemDto
    {
        public int ReunionId { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectDescription { get; set; }
        public int? AreaScopeId { get; set; }
        public string? AreaScopeDescripcion { get; set; }
        public int Numero { get; set; }
        public string Tema { get; set; } = null!;
        public string? Lugar { get; set; }
        public DateOnly Fecha { get; set; }
        public TimeOnly? HoraInicio { get; set; }
        public TimeOnly? HoraFin { get; set; }
        public int ReunionEstadoId { get; set; }
        public string ReunionEstado { get; set; } = null!;
        public int TotalAcuerdos { get; set; }
        public int AcuerdosCumplidos { get; set; }
        public int VecesReprogramada { get; set; }
        public int TotalArchivos { get; set; }
    }

    public class ReunionPaginaInicialDto
    {
        public List<ProyectoFiltroDto> Proyectos { get; set; } = new();
        public List<CatalogoDto> ReunionEstados { get; set; } = new();
        public List<TrabajadorAbrilDto> Trabajadores { get; set; } = new();
        /// <summary>Temas predefinidos para el desplegable de "Tema de la reunión" al agendar.</summary>
        public List<ReunionTemaOpcionDto> Temas { get; set; } = new();
        public PagedResultDto<ReunionListItemDto> Reuniones { get; set; } = new();
    }

    // ── Detalle ──────────────────────────────────────────────────────────────
    public class ReunionDetalleDto
    {
        public int ReunionId { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectDescription { get; set; }
        public int? AreaScopeId { get; set; }
        public string? AreaScopeDescripcion { get; set; }
        public int Numero { get; set; }
        public string Tema { get; set; } = null!;
        public string? ConvocadoPor { get; set; }
        public string? Lugar { get; set; }
        public DateOnly Fecha { get; set; }
        public TimeOnly? HoraInicio { get; set; }
        public TimeOnly? HoraFin { get; set; }
        public int ReunionEstadoId { get; set; }
        public string ReunionEstado { get; set; } = null!;
        public string? Observaciones { get; set; }

        public int? ReunionAnteriorId { get; set; }
        public int? ReunionAnteriorNumero { get; set; }
        public string? ReunionAnteriorTema { get; set; }
        public int? ReunionSiguienteId { get; set; }
        public int? ReunionSiguienteNumero { get; set; }
        public string? ReunionSiguienteTema { get; set; }

        public List<ReunionParticipanteDto> Participantes { get; set; } = new();
        public List<ReunionAcuerdoDto> Acuerdos { get; set; } = new();
        public List<ReunionArchivoDto> Archivos { get; set; } = new();
        public List<ReunionReprogramacionDto> Reprogramaciones { get; set; } = new();
        public List<CatalogoDto> AcuerdoEstados { get; set; } = new();
        public List<TrabajadorAbrilDto> Trabajadores { get; set; } = new();
        /// <summary>Temas predefinidos para el desplegable al "Agendar siguiente reunión".</summary>
        public List<ReunionTemaOpcionDto> Temas { get; set; } = new();
    }

    public class ReunionParticipanteDto
    {
        public int ReunionParticipanteId { get; set; }
        public int? WorkerId { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Cargo { get; set; }
        public string? Iniciales { get; set; }
        public bool Asistio { get; set; }
        public int Orden { get; set; }
    }

    /// <summary>Responsable de un acuerdo, con su estado de aceptación individual.</summary>
    public class ReunionAcuerdoResponsableDto
    {
        public int ReunionAcuerdoResponsableId { get; set; }
        public int WorkerId { get; set; }
        public string WorkerNombre { get; set; } = null!;
        /// <summary>PENDIENTE | ACEPTADO | RECHAZADO.</summary>
        public string EstadoAceptacion { get; set; } = null!;
        public string? MotivoRechazo { get; set; }
    }

    public class ReunionAcuerdoDto
    {
        public int ReunionAcuerdoId { get; set; }
        public string Descripcion { get; set; } = null!;
        public string? Acciones { get; set; }
        public DateOnly? FechaProgramada { get; set; }
        public DateOnly? FechaReprogramacion { get; set; }
        public DateOnly? FechaCumplimiento { get; set; }
        public int ReunionAcuerdoEstadoId { get; set; }
        public string ReunionAcuerdoEstado { get; set; } = null!;
        public int Orden { get; set; }
        public bool RequiereAceptacion { get; set; }
        public bool RequiereEvidencia { get; set; }
        public string? EvidenciaUrl { get; set; }
        public List<ReunionAcuerdoResponsableDto> Responsables { get; set; } = new();
    }

    public class ReunionArchivoDto
    {
        public int ReunionArchivoId { get; set; }
        public string ArchivoUrl { get; set; } = null!;
        public string? OriginalFileName { get; set; }
        public DateTime CreatedDateTime { get; set; }
    }

    public class ReunionReprogramacionDto
    {
        public int ReunionReprogramacionId { get; set; }
        public DateOnly FechaAnterior { get; set; }
        public TimeOnly? HoraInicioAnterior { get; set; }
        public TimeOnly? HoraFinAnterior { get; set; }
        public DateOnly FechaNueva { get; set; }
        public TimeOnly? HoraInicioNueva { get; set; }
        public TimeOnly? HoraFinNueva { get; set; }
        public string? Motivo { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public string? CreatedUserName { get; set; }
    }

    // ── Requests ─────────────────────────────────────────────────────────────
    public class ReunionParticipanteInput
    {
        /// <summary>Null cuando es un participante nuevo.</summary>
        public int? ReunionParticipanteId { get; set; }
        /// <summary>
        /// workers.id cuando el participante se eligió del desplegable de trabajadores de Abril.
        /// Si el worker no tiene puesto, el Cargo ingresado a mano se da de alta en el catálogo `puesto`.
        /// </summary>
        public int? WorkerId { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Cargo { get; set; }
        public string? Iniciales { get; set; }
        public bool Asistio { get; set; }
    }

    public class ReunionCreateRequest
    {
        /// <summary>Reunión de proyecto. Exactamente uno de ProjectId/AreaScopeId debe venir informado, o ninguno (reunión de toda la organización).</summary>
        public int? ProjectId { get; set; }
        /// <summary>Reunión de un nodo del árbol area_scope (gerencia/área/subárea).</summary>
        public int? AreaScopeId { get; set; }
        public string Tema { get; set; } = null!;
        /// <summary>Tema del catálogo elegido (null si es personalizado), para heredar su configuración de agenda/recordatorio.</summary>
        public int? ReunionTemaId { get; set; }
        public string? ConvocadoPor { get; set; }
        public string? Lugar { get; set; }
        public DateOnly Fecha { get; set; }
        public TimeOnly? HoraInicio { get; set; }
        public TimeOnly? HoraFin { get; set; }
        public int? ReunionAnteriorId { get; set; }
        public List<ReunionParticipanteInput> Participantes { get; set; } = new();
    }

    public class ReunionUpdateRequest
    {
        public string Tema { get; set; } = null!;
        public string? ConvocadoPor { get; set; }
        public string? Lugar { get; set; }
        public TimeOnly? HoraInicio { get; set; }
        public TimeOnly? HoraFin { get; set; }
        public string? Observaciones { get; set; }
        /// <summary>Lista completa de participantes: los existentes que no vengan se eliminan (soft delete).</summary>
        public List<ReunionParticipanteInput> Participantes { get; set; } = new();
    }

    public class ReunionReprogramarRequest
    {
        public DateOnly Fecha { get; set; }
        public TimeOnly? HoraInicio { get; set; }
        public TimeOnly? HoraFin { get; set; }
        public string? Motivo { get; set; }
    }

    // ── Carpeta de SharePoint para adjuntos ──────────────────────────────────

    /// <summary>Carpeta única (singleton) configurada para guardar los adjuntos de las actas.</summary>
    public class ReunionFolderDto
    {
        public int ReunionFolderId { get; set; }
        public string LinkUrl { get; set; } = null!;
        public string DriveId { get; set; } = null!;
        public string FolderId { get; set; } = null!;
        public string? FolderName { get; set; }
        public string? WebUrl { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
    }

    /// <summary>Datos para configurar/actualizar la carpeta única: solo el link pegado por el usuario.</summary>
    public class ReunionFolderSaveDto
    {
        public string LinkUrl { get; set; } = null!;
    }

    public class TemaCreateRequest
    {
        public string Descripcion { get; set; } = null!;
    }

    /// <summary>Convocatoria recurrente asociada a un tema (ej. "Reunión de Jefaturas de Proyectos").</summary>
    public class TemaConvocatoriaDto
    {
        public int? AreaScopeId { get; set; }
        public string? AreaScopeDescripcion { get; set; }
        public List<int> PuestoIds { get; set; } = new();
        public bool RequiereAgenda { get; set; }
        public bool AgendaFija { get; set; }
        public string? AgendaTexto { get; set; }
        public decimal? RecordatorioHorasAntes { get; set; }
    }

    public class TemaConvocatoriaSaveRequest
    {
        public int? AreaScopeId { get; set; }
        public List<int> PuestoIds { get; set; } = new();
        public bool RequiereAgenda { get; set; }
        public bool AgendaFija { get; set; }
        public string? AgendaTexto { get; set; }
        public decimal? RecordatorioHorasAntes { get; set; }
    }

    // ── Agenda de reunión ────────────────────────────────────────────────────
    public class ReunionAgendaItemDto
    {
        public int ReunionAgendaItemId { get; set; }
        public int WorkerId { get; set; }
        public string WorkerNombre { get; set; } = null!;
        public string Descripcion { get; set; } = null!;
        public int Orden { get; set; }
    }

    /// <summary>Agenda de una reunión concreta: fija (texto único) o dinámica (temas por participante).</summary>
    public class ReunionAgendaDto
    {
        public bool RequiereAgenda { get; set; }
        public bool AgendaFija { get; set; }
        /// <summary>Texto único cuando AgendaFija es true.</summary>
        public string? AgendaTexto { get; set; }
        /// <summary>Temas cargados por cada participante cuando AgendaFija es false.</summary>
        public List<ReunionAgendaItemDto> Items { get; set; } = new();
        /// <summary>Participantes convocados (con workerId) que aún no cargaron ningún tema.</summary>
        public List<string> ParticipantesPendientes { get; set; } = new();
        /// <summary>WorkerId del usuario autenticado que consulta, si es participante de esta reunión (para saber "mis temas").</summary>
        public int? WorkerIdActual { get; set; }
    }

    public class ReunionAgendaItemInput
    {
        public string Descripcion { get; set; } = null!;
    }

    /// <summary>Reemplaza por completo los temas a tratar del worker autenticado para una reunión.</summary>
    public class GuardarMisTemasRequest
    {
        public List<ReunionAgendaItemInput> Temas { get; set; } = new();
    }

    // ── Recordatorio de agenda (job) ─────────────────────────────────────────
    /// <summary>Reunión PROGRAMADA con agenda dinámica pendiente de recordatorio.</summary>
    public class ReunionRecordatorioCandidatoDto
    {
        public int ReunionId { get; set; }
        public int Numero { get; set; }
        public string Tema { get; set; } = null!;
        public string AmbitoDescripcion { get; set; } = null!;
        public DateOnly Fecha { get; set; }
        public TimeOnly HoraInicio { get; set; }
        public decimal RecordatorioHorasAntes { get; set; }
        public List<ReunionRecordatorioDestinatarioDto> Destinatarios { get; set; } = new();
    }

    public class ReunionRecordatorioDestinatarioDto
    {
        public int UserId { get; set; }
        public int WorkerId { get; set; }
        public string Nombre { get; set; } = null!;
        public string Email { get; set; } = null!;
    }

    // ── Convocatoria inmediata (al agendar) ──────────────────────────────────
    /// <summary>Datos de la reunión recién creada para armar el correo de convocatoria.</summary>
    public class ReunionConvocatoriaInfoDto
    {
        public int ReunionId { get; set; }
        public int Numero { get; set; }
        public string Tema { get; set; } = null!;
        public string AmbitoDescripcion { get; set; } = null!;
        public DateOnly Fecha { get; set; }
        public TimeOnly? HoraInicio { get; set; }
        public string? Lugar { get; set; }
        public List<ReunionRecordatorioDestinatarioDto> Destinatarios { get; set; } = new();
    }

    public class ReunionCambiarEstadoRequest
    {
        /// <summary>Descripción del estado destino: PROGRAMADA, REALIZADA o CANCELADA.</summary>
        public string Estado { get; set; } = null!;
    }

    public class ReunionAcuerdoRequest
    {
        public string Descripcion { get; set; } = null!;
        public string? Acciones { get; set; }
        public DateOnly? FechaProgramada { get; set; }
        public DateOnly? FechaReprogramacion { get; set; }
        public DateOnly? FechaCumplimiento { get; set; }
        /// <summary>Null al crear: se asigna PENDIENTE.</summary>
        public int? ReunionAcuerdoEstadoId { get; set; }
        /// <summary>Si true, cada responsable debe aceptar el acuerdo antes de quedar activo.</summary>
        public bool RequiereAceptacion { get; set; }
        /// <summary>Si true, no se puede marcar CUMPLIDO sin adjuntar evidencia.</summary>
        public bool RequiereEvidencia { get; set; }
        public string? EvidenciaUrl { get; set; }
        /// <summary>Ids de workers (cualquier trabajador de la organización, haya asistido o no) responsables del acuerdo.</summary>
        public List<int> ResponsableWorkerIds { get; set; } = new();
    }
}
