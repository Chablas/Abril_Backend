namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion
{
    public class ProgramacionFilterDto
    {
        public DateOnly? FechaDesde { get; set; }
        public DateOnly? FechaHasta { get; set; }
        public string? Estado { get; set; }
        public int? WorkerId { get; set; }
        public int? ClinicaId { get; set; }
        // true = médico SSOMA ve todas (incluyendo con interconsulta pendiente)
        // false/null = clínica solo ve las que no tienen interconsulta pendiente
        public bool IncluirConInterconsulta { get; set; } = false;
    }
}
