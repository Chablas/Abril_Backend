namespace Abril_Backend.Infrastructure.Models {
    public class Lesson {
        public int LessonId {get; set;}
        public string? LessonCode {get; set;}
        public string Period {get;set;}
        public DateTime? PeriodDate {get;set;}
        public string? ProblemDescription {get; set;}
        public string? ReasonDescription {get; set;}
        public string? LessonDescription {get; set;}
        public string? ImpactDescription {get; set;}
        public int? ProjectId {get; set;}
        public int AreaId {get; set;}
        public int? PhaseStageSubStageSubSpecialtyId {get; set;}
        public int StateId {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}