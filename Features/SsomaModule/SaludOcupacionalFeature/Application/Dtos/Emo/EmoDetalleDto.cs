namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Emo
{
    public class EmoDetalleDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string? WorkerNombre { get; set; }
        public string? WorkerDni { get; set; }
        public int? TipoEmoId { get; set; }
        public string? TipoEmoNombre { get; set; }
        public int? EmpresaOrigenId { get; set; }
        public string? EmpresaOrigenNombre { get; set; }
        public DateOnly FechaEmo { get; set; }
        public DateOnly? FechaVencimiento { get; set; }
        public DateOnly? FechaVencimientoCalculada { get; set; }
        public int? ClinicaId { get; set; }
        public string? ClinicaNombre { get; set; }
        public int? MedicoId { get; set; }
        public string? MedicoNombre { get; set; }
        public string? Aptitud { get; set; }
        public bool RequiereInterconsulta { get; set; }
        public string? NumeroInforme { get; set; }
        public string? UrlResultado { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? Notas { get; set; }
        public bool Activo { get; set; }
        public int? DiasParaVencer { get; set; }
        public List<EmoExamenDetalleDto> Examenes { get; set; } = new();
        public List<EmoRestriccionDetalleDto> Restricciones { get; set; } = new();
        public List<EmoConvalidacionResumenDto> Convalidaciones { get; set; } = new();
    }
}
