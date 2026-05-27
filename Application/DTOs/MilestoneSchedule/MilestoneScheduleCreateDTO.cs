namespace Abril_Backend.Application.DTOs {
    public class MilestoneScheduleCulminarRequest {
        public DateOnly? FechaRealFin { get; set; }
    }

    public class MilestoneScheduleCreateDTO {
        public int MilestoneId {get; set;}
        //public int MilestoneScheduleHistoryId {get; set;}
        public int Order {get;set;}
        public DateOnly PlannedStartDate {get; set;}
        public DateOnly? PlannedEndDate {get; set;}
    }
}