using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Application.DTOs
{
    public class LessonCreateDTO
    {
        public string ProblemDescription { get; set; }
        public string ReasonDescription { get; set; }
        public string LessonDescription { get; set; }
        public string ImpactDescription { get; set; }
        public int ProjectId { get; set; }
        public int AreaId { get; set; }
        public int PhaseId { get; set; }
        public int? StageId { get; set; }
        public int? LayerId { get; set; }
        public int? SubStageId { get; set; }
        public int? SubSpecialtyId { get; set; }
        public List<IFormFile>? OpportunityImages { get; set; }
        public List<IFormFile>? ImprovementImages { get; set; }
    }
}