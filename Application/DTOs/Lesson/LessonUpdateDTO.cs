using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Application.DTOs {
    public class UpdateLessonDTO {
        public int LessonId {get; set;}
        public string? LessonCode {get; set;}
        public string ProblemDescription {get; set;}
        public string ReasonDescription {get; set;}
        public string LessonDescription {get; set;}
        public string ImpactDescription {get; set;}
        public int ProjectId {get; set;}
        public int PhaseStageSubStageSubSpecialtyId {get; set;}
        public IFormFile[] ProblemImages { get; set; }
        public IFormFile[] LessonImages { get; set; }
    }
}