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

    /// <summary>Trabajador de Abril (workers con email_corporativo @abril.pe) para los desplegables.</summary>
    public class TrabajadorAbrilDto
    {
        public int WorkerId { get; set; }
        public string FullName { get; set; } = null!;
        /// <summary>workers.puesto; si está vacío se hace fallback a workers.ocupacion.</summary>
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
        public int? ReunionEstadoId { get; set; }
        public DateOnly? Desde { get; set; }
        public DateOnly? Hasta { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class ReunionListItemDto
    {
        public int ReunionId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
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
        public PagedResultDto<ReunionListItemDto> Reuniones { get; set; } = new();
    }

    // ── Detalle ──────────────────────────────────────────────────────────────
    public class ReunionDetalleDto
    {
        public int ReunionId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
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
    }

    public class ReunionParticipanteDto
    {
        public int ReunionParticipanteId { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Cargo { get; set; }
        public string? Iniciales { get; set; }
        public bool Asistio { get; set; }
        public int Orden { get; set; }
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
        public List<int> ResponsableIds { get; set; } = new();
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
        /// Si el worker no tiene puesto ni ocupacion, el Cargo ingresado a mano se guarda en workers.puesto.
        /// </summary>
        public int? WorkerId { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Cargo { get; set; }
        public string? Iniciales { get; set; }
        public bool Asistio { get; set; }
    }

    public class ReunionCreateRequest
    {
        public int ProjectId { get; set; }
        public string Tema { get; set; } = null!;
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
        /// <summary>Ids de reunion_participante responsables de ejecutar el acuerdo.</summary>
        public List<int> ResponsableIds { get; set; } = new();
    }
}
