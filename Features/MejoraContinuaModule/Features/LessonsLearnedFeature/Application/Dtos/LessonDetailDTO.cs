namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos
{
    public class LessonDetailDTO
    {
        public int LessonId { get; set; }
        public string? LessonCode { get; set; }
        public string Period { get; set; } = string.Empty;
        public string? ProblemDescription { get; set; }
        public string? ReasonDescription { get; set; }
        public string? LessonDescription { get; set; }
        public string? ImpactDescription { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectDescription { get; set; }
        public int AreaId { get; set; }
        /// <summary>Path completo del área (incluye Gerencia). Mostrado en el modal de detalle.</summary>
        public string? AreaDescription { get; set; }
        /// <summary>Path "corto" sin Gerencia, en MAYÚSCULAS. Espejo del campo en LessonListDTO.</summary>
        public string? AreaListDescription { get; set; }
        public int? LessonAreaId { get; set; }
        public int? CatalogItemId { get; set; }
        public int? PhaseStageSubStageSubSpecialtyId { get; set; }
        public int? PhaseId { get; set; }
        public string? PhaseDescription { get; set; }
        public int? StageId { get; set; }
        public string? StageDescription { get; set; }
        public int? LayerId { get; set; }
        public string? LayerDescription { get; set; }
        public int? SubStageId { get; set; }
        public string? SubStageDescription { get; set; }
        public int? SubSpecialtyId { get; set; }
        public string? SubSpecialtyDescription { get; set; }
        public int? PartidaId { get; set; }
        public string? PartidaDescription { get; set; }
        public int StateId { get; set; }
        public string StateDescription { get; set; } = string.Empty;
        public List<LessonImageDTO>? Images { get; set; }

        public DateTimeOffset CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public string? CreatedUserFullName { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
    }

    public class LessonImageDTO
    {
        public int LessonImageId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public int LessonId { get; set; }
        public int ImageTypeId { get; set; }
        public string ImageTypeDescription { get; set; } = string.Empty;
    }
}
