namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion
{
    public class ProgramacionListDto
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public string? WorkerNombre { get; set; }
        public string? WorkerDni { get; set; }
        public string? Empresa { get; set; }
        public string? TipoEmo { get; set; }
        public DateOnly FechaProgramada { get; set; }
        public TimeOnly? HoraProgramada { get; set; }
        public string? Clinica { get; set; }
        public string? Medico { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? Motivo { get; set; }
        public int? EmoResultadoId { get; set; }
    }
}
