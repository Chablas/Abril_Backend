namespace Abril_Backend.Features.GestionAdministrativa.SolicitudSalidas.Infrastructure.Models
{
    /// <summary>
    /// Solicitud de salida — cabecera. Los detalles del/los trayecto(s) viven en GaSolicitudTrayecto.
    /// </summary>
    public class GaSolicitudSalida
    {
        public int Id { get; set; }
        public int WorkerId { get; set; }
        public DateOnly FechaSalida { get; set; }
        public string EstadoAprobacion { get; set; } = "Pendiente";
        public string EstadoRendicion { get; set; } = "No rendido";
        public int? RegistradoPorId { get; set; }
        public string? AprobadorEmail { get; set; }
        public DateTimeOffset? FechaDecision { get; set; }
        public string? MotivoRechazo { get; set; }
        public int? RendicionId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
