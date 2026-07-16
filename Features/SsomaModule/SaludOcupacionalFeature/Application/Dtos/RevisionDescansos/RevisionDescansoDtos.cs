namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.RevisionDescansos
{
    /// <summary>
    /// Fila de la tabla de revisión de solicitudes de descanso médico
    /// (solo descansos reportados por el propio trabajador desde Mi Salud).
    /// </summary>
    public class RevisionDescansoListItemDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string? WorkerNombre { get; set; }
        public string? WorkerDni { get; set; }
        public string? AreaNombre { get; set; }
        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaFin { get; set; }
        public int Dias { get; set; }
        public string? Motivo { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int AdjuntosCount { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class RevisionDescansoAdjuntoDto
    {
        public string Url { get; set; } = string.Empty;
        public string? Nombre { get; set; }
    }

    public class RevisionDescansoDetalleDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string? WorkerNombre { get; set; }
        public string? WorkerDni { get; set; }
        public string? AreaNombre { get; set; }
        public string? EmpresaNombre { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaFin { get; set; }
        public int Dias { get; set; }
        public string? Motivo { get; set; }
        public string? Diagnostico { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? MotivoRechazo { get; set; }
        public string? AprobadoPorNombre { get; set; }
        public DateTimeOffset? FechaAprobacion { get; set; }
        public string? Observaciones { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public List<RevisionDescansoAdjuntoDto> Adjuntos { get; set; } = [];
    }

    public class RevisionDescansosFiltroDto
    {
        public int? WorkerId { get; set; }
        public string? Estado { get; set; }
        public DateOnly? FechaDesde { get; set; }
        public DateOnly? FechaHasta { get; set; }
        /// <summary>area_scope_id del nodo elegido + sus descendientes (los arma el frontend).</summary>
        public List<int>? AreaScopeIds { get; set; }
        public string? SortBy { get; set; }
        public string? SortDir { get; set; }
        public int Page { get; set; } = 1;
    }

    /// <summary>Nodo plano del árbol de áreas (area_scope) para el filtro en cascada.</summary>
    public class RevisionAreaNodoDto
    {
        public int AreaScopeId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public int? AreaScopeParentId { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class RevisionTrabajadorOpcionDto
    {
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
    }

    /// <summary>
    /// Carga inicial de la pantalla en un solo roundtrip: datos de filtros + primera página.
    /// </summary>
    public class RevisionDescansosInitDto
    {
        public List<RevisionAreaNodoDto> AreaTree { get; set; } = [];
        public List<RevisionTrabajadorOpcionDto> Trabajadores { get; set; } = [];
        public Abril_Backend.Application.DTOs.PagedResult<RevisionDescansoListItemDto> Tabla { get; set; } = new();
    }

    public class RevisionDescansosAprobarDto
    {
        public List<int> Ids { get; set; } = [];
    }

    public class RevisionDescansosRechazarDto
    {
        public List<int> Ids { get; set; } = [];
        public string MotivoRechazo { get; set; } = string.Empty;
    }
}
