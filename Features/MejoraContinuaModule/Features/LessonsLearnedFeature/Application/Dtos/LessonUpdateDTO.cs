using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos
{
    public class UpdateLessonDTO
    {
        public int LessonId { get; set; }
        public string? LessonCode { get; set; }
        public string ProblemDescription { get; set; } = string.Empty;
        public string ReasonDescription { get; set; } = string.Empty;
        public string LessonDescription { get; set; } = string.Empty;
        public string ImpactDescription { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public int PhaseStageSubStageSubSpecialtyId { get; set; }
        public IFormFile[] ProblemImages { get; set; } = Array.Empty<IFormFile>();
        public IFormFile[] LessonImages { get; set; } = Array.Empty<IFormFile>();
    }
}
