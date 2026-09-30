namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ContratosFeature.Infrastructure.Models {
    /// <summary>Hito de pago de un ProjectContract — libre por contrato (nombre/%/cantidad varían,
    /// a diferencia de los hitos de proyecto de MilestoneScheduleFeature, que sí son un catálogo
    /// fijo). El hito de garantía (cláusula Octava del contrato) siempre es el último por Order,
    /// nunca uno marcado aparte.</summary>
    public class ProjectContractMilestone {
        public int ProjectContractMilestoneId {get; set;}
        public int ProjectContractId {get; set;}
        public int Order {get; set;}
        /// <summary>Texto libre, ej. "Hito 1: Al cierre de etapa de arquitectura + estructura V.1 + MEP V.1."</summary>
        public string Description {get; set;} = null!;
        public decimal Percentage {get; set;}
        public decimal? Amount {get; set;}
        public DateOnly? PaidDate {get; set;}
        public string? ChequeRecibo {get; set;}
        public string? Observation {get; set;}

        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}
