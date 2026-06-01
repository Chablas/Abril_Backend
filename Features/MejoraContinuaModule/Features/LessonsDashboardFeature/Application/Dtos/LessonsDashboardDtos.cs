namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsDashboardFeature.Application.Dtos
{
    public class ChartItemDTO
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class PhaseStageChartDTO
    {
        public int PhaseId { get; set; }
        public string PhaseLabel { get; set; } = string.Empty;
        public List<ChartItemDTO> Stages { get; set; } = new();
    }

    /// <summary>Tarjetas resumen del dashboard.</summary>
    public class DashboardSummaryDTO
    {
        public int TotalLessons { get; set; }
        public int TotalProjects { get; set; }     // proyectos distintos con lecciones
        public int TotalAreas { get; set; }        // áreas (lesson_area) distintas con lecciones
        public int TotalPhases { get; set; }       // fases distintas con lecciones
        public int TotalUsers { get; set; }        // usuarios distintos que registraron lecciones
    }

    public class LessonsDashboardDataDTO
    {
        public DashboardSummaryDTO Summary { get; set; } = new();
        public List<ChartItemDTO> LessonsByMonth { get; set; } = new();
        public List<ChartItemDTO> LessonsByProject { get; set; } = new();
        public List<ChartItemDTO> LessonsByArea { get; set; } = new();
        public List<ChartItemDTO> LessonsByPhase { get; set; } = new();
        public List<PhaseStageChartDTO> LessonsByPhaseAndStage { get; set; } = new();
        public List<ChartItemDTO> LessonsBySubStage { get; set; } = new();
    }

    public class DashboardPeriodDTO
    {
        public DateTimeOffset? PeriodDate { get; set; }
    }

    public class DashboardUserDTO
    {
        public int UserId { get; set; }
        public string? FullName { get; set; }
    }

    public class DashboardAreaDTO
    {
        public int LessonAreaId { get; set; }
        public string AreaDescription { get; set; } = string.Empty;
    }

    public class LessonsDashboardFiltersDTO
    {
        public List<DashboardPeriodDTO> Periods { get; set; } = new();
        public List<DashboardUserDTO> Users { get; set; } = new();
        public List<DashboardAreaDTO> Areas { get; set; } = new();
    }
}
