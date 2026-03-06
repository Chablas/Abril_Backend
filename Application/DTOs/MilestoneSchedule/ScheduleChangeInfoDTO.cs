namespace Abril_Backend.Application.DTOs
{
    public class ScheduleChangeInfoDTO
    {
        public string ProjectDescription { get; set; }
        public string ChangedBy { get; set; }
        public List<DateTime> ChangeDate { get; set; }
    }
}