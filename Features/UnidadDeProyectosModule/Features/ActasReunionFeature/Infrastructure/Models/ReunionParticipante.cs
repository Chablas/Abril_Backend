namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models
{
    /// <summary>
    /// Participante convocado a una reunión. Las iniciales identifican al participante
    /// como responsable dentro de los acuerdos (columna EJECUCIÓN del formato SIG-FO-17).
    /// </summary>
    public class ReunionParticipante
    {
        public int ReunionParticipanteId { get; set; }
        public int ReunionId { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Cargo { get; set; }
        public string? Iniciales { get; set; }
        public bool Asistio { get; set; }
        public int Orden { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
