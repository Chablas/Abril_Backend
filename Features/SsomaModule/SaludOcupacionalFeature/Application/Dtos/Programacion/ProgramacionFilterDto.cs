namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion
{
    public class ProgramacionFilterDto
    {
        public DateOnly? FechaDesde { get; set; }
        public DateOnly? FechaHasta { get; set; }
        public string? Estado { get; set; }
        public int? WorkerId { get; set; }
        public int? ClinicaId { get; set; }
    }
}
