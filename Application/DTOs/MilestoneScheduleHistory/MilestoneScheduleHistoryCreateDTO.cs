namespace Abril_Backend.Application.DTOs {
    public class MilestoneScheduleHistoryCreateDTO {
        public int ProjectId {get; set;}
        public List<MilestoneScheduleCreateDTO> MilestoneSchedules {get;set;}
        public bool ForceSave { get;set; }
    }
}