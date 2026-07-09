namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ActasReunionFeature.Infrastructure.Models
{
    /// <summary>
    /// Acuerdo/acción tomada en una reunión. Tiene fecha programada, una posible
    /// reprogramación y la fecha real en la que se terminó cumpliendo.
    /// </summary>
    public class ReunionAcuerdo
    {
        public int ReunionAcuerdoId { get; set; }
        public int ReunionId { get; set; }
        public string Descripcion { get; set; } = null!;
        public string? Acciones { get; set; }
        public DateOnly? FechaProgramada { get; set; }
        public DateOnly? FechaReprogramacion { get; set; }
        public DateOnly? FechaCumplimiento { get; set; }
        public int ReunionAcuerdoEstadoId { get; set; }
        public int Orden { get; set; }

        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
