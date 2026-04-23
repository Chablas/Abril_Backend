namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Convalidacion
{
    public class ConvalidacionUpdateDto
    {
        public int? EmpresaDestinoId { get; set; }
        public DateOnly FechaConvalidacion { get; set; }
        public int? MedicoId { get; set; }
        public string Resultado { get; set; } = "Aprobada";
        public DateOnly? FechaVencimiento { get; set; }
        public string? UrlDocumento { get; set; }
        public string? Observaciones { get; set; }
    }
}
