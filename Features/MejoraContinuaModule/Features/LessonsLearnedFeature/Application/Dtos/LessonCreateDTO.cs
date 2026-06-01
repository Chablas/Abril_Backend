using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos
{
    public class LessonCreateDTO
    {
        [MaxLength(2000, ErrorMessage = "La descripción del problema no puede exceder 2000 caracteres.")]
        public string ProblemDescription { get; set; } = string.Empty;
        [MaxLength(2000, ErrorMessage = "La descripción de la causa no puede exceder 2000 caracteres.")]
        public string ReasonDescription { get; set; } = string.Empty;
        [MaxLength(2000, ErrorMessage = "La descripción de la lección no puede exceder 2000 caracteres.")]
        public string LessonDescription { get; set; } = string.Empty;
        [MaxLength(2000, ErrorMessage = "La descripción del impacto no puede exceder 2000 caracteres.")]
        public string ImpactDescription { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        // Legacy: areaId queda opcional (las lecciones nuevas usan LessonAreaId).
        public int AreaId { get; set; }
        /// <summary>Rama seleccionada (lesson_area) — reemplaza al area legacy.</summary>
        public int? LessonAreaId { get; set; }
        /// <summary>Nodo hoja seleccionado del árbol de scope (reemplaza PhaseId/StageId/etc).</summary>
        public int? CatalogItemId { get; set; }
        // Campos legacy — se mantienen para lecciones antiguas creadas antes de la migración
        public int PhaseId { get; set; }
        public int? StageId { get; set; }
        public int? LayerId { get; set; }
        public int? SubStageId { get; set; }
        public int? SubSpecialtyId { get; set; }
        public int? PartidaId { get; set; }
        public List<IFormFile>? OpportunityImages { get; set; }
        public List<IFormFile>? ImprovementImages { get; set; }
    }
}
