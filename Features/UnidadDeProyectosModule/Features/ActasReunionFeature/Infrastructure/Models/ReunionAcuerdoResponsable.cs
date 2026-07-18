namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models
{
    /// <summary>Responsable de ejecutar un acuerdo; siempre es un participante de la misma reunión.</summary>
    public class ReunionAcuerdoResponsable
    {
        public int ReunionAcuerdoResponsableId { get; set; }
        public int ReunionAcuerdoId { get; set; }
        public int ReunionParticipanteId { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
