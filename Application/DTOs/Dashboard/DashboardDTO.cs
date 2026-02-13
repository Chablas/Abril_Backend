namespace Abril_Backend.Application.DTOs
{
    public class DashboardDTO
    {
        public List<ChartItemDTO>? LessonsByPhase { get; set; }
        public List<ChartItemDTO>? LessonsByProject { get; set; }
        public List<PhaseStageChartDTO>? LessonsByPhaseAndStage { get; set; }
        public List<ChartItemDTO>? LessonsBySubStage { get; set; }
    }
}
