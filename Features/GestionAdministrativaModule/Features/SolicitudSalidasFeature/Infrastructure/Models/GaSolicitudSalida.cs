namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models
{
    public class GaSolicitudSalida
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public DateOnly FechaSalida { get; set; }
        public TimeOnly HoraSalida { get; set; }
        public TimeOnly? HoraRetorno { get; set; }
        public int? MotivoId { get; set; }
        public string? MotivoLibre { get; set; }
        public int? LugarOrigenId { get; set; }
        public string? LugarOrigenLibre { get; set; }
        public int? LugarDestinoId { get; set; }
        public string? LugarDestinoLibre { get; set; }
        public string EstadoAprobacion { get; set; } = "Pendiente";
        public string EstadoRendicion { get; set; } = "No rendido";
        public int? RegistradoPorId { get; set; }
        public string? AprobadorEmail { get; set; }
        public DateTimeOffset? FechaDecision { get; set; }
        public string? MotivoRechazo { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
