namespace Abril_Backend.Application.DTOs {
    public class MilestoneScheduleFakeDataDTO {
        public int MilestoneId {get; set;}
        public string MilestoneDescription {get;set;}
        public int Order {get;set;}
        public DateTime PlannedStartDate {get; set;}
        public DateTime? PlannedEndDate {get; set;}
    }
}