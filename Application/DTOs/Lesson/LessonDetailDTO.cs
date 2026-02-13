namespace Abril_Backend.Application.DTOs {
    public class LessonDetailDTO {
        public int LessonId {get; set;}
        public string? LessonCode {get; set;}
        public string Period {get;set;}
        public string? ProblemDescription {get; set;}
        public string? ReasonDescription {get; set;}
        public string? LessonDescription {get; set;}
        public string? ImpactDescription {get; set;}
        public int? ProjectId {get; set;}
        public string? ProjectDescription  {get; set;}
        public int AreaId {get; set;}
        public string AreaDescription {get; set;}
        public int? PhaseStageSubStageSubSpecialtyId {get; set;}
        public int? PhaseId {get;set;}
        public string? PhaseDescription {get;set;}
        public int? StageId {get;set;}
        public string? StageDescription {get;set;}
        public int? SubStageId {get;set;}
        public string? SubStageDescription {get;set;}
        public int? SubSpecialtyId {get;set;}
        public string? SubSpecialtyDescription {get;set;}
        public int StateId {get; set;}
        public string StateDescription {get; set;}
        public List<LessonImageDTO>? Images {get; set;}

        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public string? CreatedUserFullName {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
    }

    public class LessonImageDTO {
        public int LessonImageId {get; set;}
        public string ImageUrl {get; set;}
        public int LessonId {get; set;}
        public int ImageTypeId {get; set;}
        public string ImageTypeDescription {get; set;}
    }
}