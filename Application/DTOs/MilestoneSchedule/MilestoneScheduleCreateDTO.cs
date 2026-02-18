namespace Abril_Backend.Application.DTOs {
    public class MilestoneScheduleCreateDTO {
        public int MilestoneId {get; set;}
        public int milestoneScheduleHistoryId {get; set;}
        public int Order {get;set;}
        public DateTime PlannedStartDate {get; set;}
        public DateTime? PlannedEndDate {get; set;}
    }
}