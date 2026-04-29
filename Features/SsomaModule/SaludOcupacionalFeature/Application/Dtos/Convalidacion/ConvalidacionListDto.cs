namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Convalidacion
{
    public class ConvalidacionListDto
    {
        public int Id { get; set; }
        public int EmoId { get; set; }
        public int WorkerId { get; set; }
        public string? WorkerNombre { get; set; }
        public string? WorkerDni { get; set; }
        public string? EmpresaOrigen { get; set; }
        public string? EmpresaDestino { get; set; }
        public DateOnly FechaConvalidacion { get; set; }
        public string Resultado { get; set; } = string.Empty;
        public DateOnly? FechaVencimiento { get; set; }
        public int? DiasParaVencer { get; set; }
        public string? Observaciones { get; set; }
        public string? UrlDocumento { get; set; }
    }
}
