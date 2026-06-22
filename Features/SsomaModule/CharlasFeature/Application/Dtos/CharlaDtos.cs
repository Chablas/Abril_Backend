namespace Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Dtos;

// ── Existing Response DTOs ────────────────────────────────────────────────────
public record ProyectoDto(int ProyectoId, string Nombre);
public record StaffDto(int WorkerId, string NombreCompleto, string Cargo);
public record CharlaResumenDto(int Id, DateTime Fecha, string Titulo, string Tema, decimal DuracionHoras, int TotalAsistentes, List<int> AsistentesIds);
public record AsistenciaDetailDto(int WorkerId, string NombreCompleto, bool Asistio);
public record CapacitacionDto(int? Id, int WorkerId, string NombreCompleto, DateTime? Fecha, string? Tema, string? EvidenciaUrl, string? EvidenciaNombre, string Estado);
public record ResumenDto(int TotalCharlas, int TotalAsistencias, int CapsTotal, int CapsFalta, int CapsEnviado, int CapsAprobado, int CapsRechazado);

// ── Existing Request DTOs ─────────────────────────────────────────────────────
public record CrearCharlaDto(DateTime Fecha, string Titulo, string Tema, decimal DuracionHoras, int ProyectoId);
public record GuardarAsistenciaDto(List<int> WorkerIds);
public record CambiarEstadoDto(string Estado);

// ── NEW: Tab 1 — Dashboard Asistencia Supervisores ────────────────────────────
public record DashSupervisoresRowDto(
    int CharlaId,
    string Titulo,
    DateTime Fecha,
    int? SupervisorId,
    string SupervisorNombre,
    int TotalAsistentes,
    int TotalAsistio
);

// ── NEW: Tab 2 — Comparativo Programadas vs Realizadas ───────────────────────
public record ComparativoMesDto(int Mes, string MesNombre, int Programadas, int Realizadas);

// ── NEW: Tab 3 — Crear nueva charla ──────────────────────────────────────────
public record NuevaCharlaCreateDto(
    int? ProgramaId,
    int ProyectoId,
    string Titulo,
    string? Tema,
    string? Descripcion,
    DateTime Fecha,
    decimal DuracionHoras,
    int? SupervisorId,
    List<int> WorkerIds
);

// ── NEW: Tab 4 — Lista paginada ───────────────────────────────────────────────
public record CharlaListItemDto(
    int Id,
    string Titulo,
    string? Tema,
    DateTime Fecha,
    int? SupervisorId,
    string SupervisorNombre,
    string Estado,
    string? EvidenciaNombre,
    int TotalAsistentes
);
public record CharlaListResultDto(List<CharlaListItemDto> Items, int Total);

// ── NEW: Tab 4 — Detalle modal ────────────────────────────────────────────────
public record CharlaDetalleDto(
    int Id,
    string Titulo,
    string? Tema,
    string? Descripcion,
    DateTime Fecha,
    decimal DuracionHoras,
    int? SupervisorId,
    string SupervisorNombre,
    string Estado,
    string? EvidenciaUrl,
    string? EvidenciaNombre,
    int TotalAsistentes,
    List<AsistenciaDetailDto> Asistencias,
    int? AprobadoPorId,
    string? AprobadoPorNombre,
    DateTime? AprobadoEn,
    string? MotivoRechazo,
    DateTime? EvidenciaSubidaEn
);

// ── NEW: Tab 4 — Acciones ─────────────────────────────────────────────────────
public record RechazarCharlaDto(string Motivo);

// ── NEW: Supervisor search ────────────────────────────────────────────────────
public record UsuarioDto(int Id, string NombreCompleto, string? Email);
