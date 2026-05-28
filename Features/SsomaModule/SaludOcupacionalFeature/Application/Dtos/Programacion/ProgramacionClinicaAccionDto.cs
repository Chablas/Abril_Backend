namespace Abril_Backend.Features.Ssoma.SaludOcupacional.Application.Dtos.Programacion
{
    public class ProgramacionClinicaAccionDto
    {
        public int Id { get; set; }
        public string Accion { get; set; } = string.Empty;
        public string? MotivoRechazo { get; set; }
        public TimeOnly? CheckInHora { get; set; }
        public int? EmoResultadoId { get; set; }
        public DateOnly? NuevaFecha { get; set; }
    }
}
