namespace Abril_Backend.Application.DTOs
{
    public class MilestoneChange
    {
        public int MilestoneId { get; set; }
        public string ChangeType { get; set; }
        public bool OrderChanged { get; set; }
        public bool StartDateChanged { get; set; }
        public bool EndDateChanged { get; set; }
    }
}