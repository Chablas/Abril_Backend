namespace Abril_Backend.Features.SsomaModule.CharlasFeature.Application.Dtos;

// ── Response DTOs ─────────────────────────────────────────────────────────────
public record ProyectoDto(int ProyectoId, string Nombre);

public record StaffDto(int WorkerId, string NombreCompleto, string Cargo);

public record CharlaResumenDto(
    int Id,
    DateTime Fecha,
    string Titulo,
    string Tema,
    decimal DuracionHoras,
    int TotalAsistentes,
    List<int> AsistentesIds
);

public record AsistenciaDetailDto(int WorkerId, string NombreCompleto, bool Asistio);

public record CapacitacionDto(
    int? Id,
    int WorkerId,
    string NombreCompleto,
    DateTime? Fecha,
    string? Tema,
    string? EvidenciaUrl,
    string? EvidenciaNombre,
    string Estado
);

public record ResumenDto(
    int TotalCharlas,
    int TotalAsistencias,
    int CapsTotal,
    int CapsFalta,
    int CapsEnviado,
    int CapsAprobado,
    int CapsRechazado
);

// ── Request DTOs ──────────────────────────────────────────────────────────────
public record CrearCharlaDto(
    DateTime Fecha,
    string Titulo,
    string Tema,
    decimal DuracionHoras,
    int ProyectoId
);

public record GuardarAsistenciaDto(List<int> WorkerIds);

public record CambiarEstadoDto(string Estado);
