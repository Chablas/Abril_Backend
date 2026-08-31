namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Convalidacion
{
    /// <summary>
    /// Interconsulta del trabajador expuesta como un documento más del EMO al revisar la
    /// convalidación: el médico necesita leer los informes antes de resolver.
    /// </summary>
    public class ConvalidacionInterconsultaDto
    {
        public int Id { get; set; }
        public string Especialidad { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateOnly FechaDerivacion { get; set; }
        public DateOnly? FechaAtencion { get; set; }
        public string? UrlInforme { get; set; }
    }
}
