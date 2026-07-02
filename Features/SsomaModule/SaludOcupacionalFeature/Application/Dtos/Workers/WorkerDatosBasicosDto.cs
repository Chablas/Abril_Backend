namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Workers
{
    /// <summary>
    /// Edición mínima de los datos de identidad de un trabajador (modal
    /// Configuración → Trabajadores). Solo modifica la tabla <c>person</c>:
    /// nombre completo, tipo y número de documento, y cumpleaños.
    /// </summary>
    public class WorkerDatosBasicosDto
    {
        public string NombreCompleto { get; set; } = string.Empty;
        public int? DocumentIdentityTypeId { get; set; }
        public string? NumeroDocumento { get; set; }
        public DateOnly? Cumpleanos { get; set; }
    }
}
