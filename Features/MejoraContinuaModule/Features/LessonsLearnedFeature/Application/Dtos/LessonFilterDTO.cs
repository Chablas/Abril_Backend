namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos
{
    public class LessonFilterDTO
    {
        public DateTimeOffset? PeriodDate { get; set; }
        public int? StateId { get; set; }
        public int? ProjectId { get; set; }
        public int? AreaId { get; set; }
        public int? PhaseId { get; set; }
        public int? StageId { get; set; }
        public int? LayerId { get; set; }
        public int? SubStageId { get; set; }
        public int? SubSpecialtyId { get; set; }
        public int? UserId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
