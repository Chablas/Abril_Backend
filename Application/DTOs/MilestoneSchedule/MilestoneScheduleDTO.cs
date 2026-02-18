namespace Abril_Backend.Application.DTOs {
    public class MilestoneScheduleDTO {
        public int MilestoneScheduleId {get; set;}
        public int MilestoneId {get; set;}
        public string MilestoneDescription {get;set;}
        public int MilestoneScheduleHistoryId {get;set;}
        public int Order {get;set;}
        public DateTime? PlannedStartDate {get; set;}
        public DateTime? PlannedEndDate {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
    }
}