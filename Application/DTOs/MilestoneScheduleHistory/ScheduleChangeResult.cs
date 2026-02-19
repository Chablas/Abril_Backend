namespace Abril_Backend.Application.DTOs
{
    public class ScheduleChangeResult
    {
        public string ProjectName { get; set; } = string.Empty;
        public string ScheduleName { get; set; } = string.Empty;
        public List<MilestoneChange> Changes { get; set; } = new();
    }
}