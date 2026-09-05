namespace Abril_Backend.Infrastructure.Models {
    public class Milestone {
        public int MilestoneId {get; set;}
        public string MilestoneDescription {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
        /// <summary>Si es true, el hito debe tener sí o sí PlannedEndDate: el cronograma se
        /// rechaza si llega sin fecha para este hito (ver MilestoneScheduleHistoryRepository.Create).</summary>
        public bool EsObligatorio {get; set;} = false;
        /// <summary>Si es true, el hito es de una sola fecha de cumplimiento (no un rango de
        /// inicio-fin) — no debería usar PlannedStartDate.</summary>
        public bool EsPuntual {get; set;} = false;
    }
}