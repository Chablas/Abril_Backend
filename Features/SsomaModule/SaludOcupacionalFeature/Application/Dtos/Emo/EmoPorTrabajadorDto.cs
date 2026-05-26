namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo
{
    public class EmoPorTrabajadorDto
    {
        public int WorkerId { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Dni { get; set; } = string.Empty;
        public int? EmpresaId { get; set; }
        public string? Empresa { get; set; }
        public string? EmpresaOrigenNombre { get; set; }
        public string? ProyectoNombre { get; set; }
        public string? ObraOficina { get; set; }
        public string? TipoContrata { get; set; }
        public bool TieneEmo { get; set; }
        public int? EmoId { get; set; }
        public string? TipoEmo { get; set; }
        public DateOnly? FechaEmo { get; set; }
        public DateOnly? FechaVencimiento { get; set; }
        public string? Aptitud { get; set; }
        public string? Estado { get; set; }
        public int? DiasRestantes { get; set; }
    }
}
