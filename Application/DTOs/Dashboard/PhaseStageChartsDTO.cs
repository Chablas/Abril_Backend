namespace Abril_Backend.Application.DTOs
{
    public class PhaseStageChartDTO
    {
        public int PhaseId { get; set; }
        public string PhaseLabel { get; set; } = null!;
        public List<ChartItemDTO> Stages { get; set; } = new();
    }
}
