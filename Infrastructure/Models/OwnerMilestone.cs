namespace Abril_Backend.Infrastructure.Models {
    /// <summary>Catálogo fijo de los hitos que se muestran al propietario/comprador (vista
    /// simplificada del cronograma) — no es por proyecto, existe una sola vez. Cada fila apunta a
    /// un hito del catálogo interno (Milestone) del que toma PlannedStartDate/PlannedEndDate; ver
    /// MilestoneScheduleRepository.GetOwnerMilestonesByHistoryIdAsync. Si el hito interno referenciado
    /// no tiene fila en milestone_schedule para la versión consultada, las fechas simplemente
    /// salen null — no bloquea ni exige nada (a diferencia de EsObligatorio en Milestone).</summary>
    public class OwnerMilestone {
        public int OwnerMilestoneId {get; set;}
        public string Description {get; set;}
        /// <summary>Hito del catálogo interno (Milestone) del que se toman las fechas.</summary>
        public int MilestoneId {get; set;}
        public int Order {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}
