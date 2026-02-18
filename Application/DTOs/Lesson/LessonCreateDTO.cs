using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Abril_Backend.Application.DTOs
{
    public class LessonCreateDTO
    {
        [MaxLength(2000, ErrorMessage = "La descripción del problema no puede exceder 2000 caracteres.")]
        public string ProblemDescription { get; set; }
        [MaxLength(2000, ErrorMessage = "La descripción de la causa no puede exceder 2000 caracteres.")]
        public string ReasonDescription { get; set; }
        [MaxLength(2000, ErrorMessage = "La descripción de la lección no puede exceder 2000 caracteres.")]
        public string LessonDescription { get; set; }
        [MaxLength(2000, ErrorMessage = "La descripción del impacto no puede exceder 2000 caracteres.")]
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