namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers
{
    /// <summary>
    /// Edición de un trabajador desde el modal Configuración → Trabajadores.
    /// Modifica la tabla <c>person</c> (nombre completo, tipo y número de documento,
    /// cumpleaños) y campos de puesto en <c>workers</c> (categoría, ocupación y el
    /// puesto final autocompletado).
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
    }
}
