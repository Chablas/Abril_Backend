namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos
{
    public class LessonFilterDTO
    {
        public DateTimeOffset? PeriodDate { get; set; }
        public int? StateId { get; set; }
        public int? ProjectId { get; set; }
        /// <summary>lesson_area_id (modelo nuevo, ramas activas).</summary>
        public int? AreaId { get; set; }
        public int? UserId { get; set; }
        /// <summary>
        /// IDs de catalog_item seleccionados en los filtros por categoría
        /// (uno por catalog_type). Una lección matchea si TODOS estos IDs
        /// aparecen en el ancestor chain (scope_item) de su catalog_item.
        /// </summary>
        public List<int>? CatalogItemIds { get; set; }
        /// <summary>Filtro por estado de aprobación: PENDIENTE | APROBADA | RECHAZADA.</summary>
        public string? ApprovalStatus { get; set; }
        /// <summary>Si true, solo lecciones PENDIENTES cuyo autor tiene como Jefe al usuario actual.</summary>
        public bool OnlyMyPendingReview { get; set; }
        /// <summary>user_id del usuario que consulta (para "pendientes de mi revisión").</summary>
        public int CurrentUserId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
