namespace Abril_Backend.Infrastructure.Models {
    public class MilestoneSchedule {
        public int MilestoneScheduleId {get; set;}
        public int MilestoneId {get; set;}
        public int MilestoneScheduleHistoryId {get;set;}
        public DateTime? PlannedStartDate {get; set;}
        public DateTime? PlannedEndDate {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}