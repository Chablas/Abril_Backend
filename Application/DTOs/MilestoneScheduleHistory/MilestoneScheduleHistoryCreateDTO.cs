namespace Abril_Backend.Application.DTOs {
    public class MilestoneScheduleHistoryCreateDTO {
        public int ScheduleId {get; set;}
        public List<MilestoneScheduleCreateDTO> MilestoneSchedules {get;set;}
    }
}