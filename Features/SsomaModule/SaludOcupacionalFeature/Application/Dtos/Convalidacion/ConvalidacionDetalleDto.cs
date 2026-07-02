namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Convalidacion
{
    public class ConvalidacionDetalleDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string WorkerNombre { get; set; } = string.Empty;
        public string WorkerDni { get; set; } = string.Empty;
        public string? WorkerOcupacion { get; set; }
        public string? TipoEmo { get; set; }
        public DateOnly? FechaEmoOrigen { get; set; }
        public string? AptitudOrigen { get; set; }
        public string? EmpresaOrigen { get; set; }
        public string? EmpresaDestino { get; set; }
        public string? MedicoNombre { get; set; }
        public string? MedicoEspecialidad { get; set; }
        public string? MedicoRegistroCmp { get; set; }
        public DateOnly FechaConvalidacion { get; set; }
        public DateOnly? FechaVencimiento { get; set; }
        public string Resultado { get; set; } = string.Empty;
        public string? Notas { get; set; }
    }
}
