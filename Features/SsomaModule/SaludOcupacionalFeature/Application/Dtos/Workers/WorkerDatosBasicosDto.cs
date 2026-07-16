namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers
{
    /// <summary>
    /// Edición de un trabajador desde el modal Configuración → Trabajadores.
    /// Modifica la tabla <c>person</c> (nombre completo, tipo y número de documento,
    /// cumpleaños) y campos de puesto/área en <c>workers</c> (categoría, ocupación,
    /// el puesto final autocompletado y el área asignada en el árbol de áreas).
    /// </summary>
    public class WorkerDatosBasicosDto
    {
        public string NombreCompleto { get; set; } = string.Empty;
        public int? DocumentIdentityTypeId { get; set; }
        public string? NumeroDocumento { get; set; }
        public DateOnly? Cumpleanos { get; set; }
        public string? Categoria { get; set; }
        public string? Ocupacion { get; set; }
        public int? OcupacionId { get; set; }
        /// <summary>Nombre del puesto final (autocompletado de Categoría + Ocupación, editable).</summary>
        public string? Puesto { get; set; }
        /// <summary>Nodo del árbol de áreas asignado al trabajador (workers.area_scope_id). Null = sin área.</summary>
        public int? AreaScopeId { get; set; }
        /// <summary>FK al catálogo <c>workers_category</c> (workers.worker_category_id). Null = sin categoría normalizada.</summary>
        public int? WorkerCategoryId { get; set; }
    }
}
