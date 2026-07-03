using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos
{
    public class LessonFiltersFormDataDTO
    {
        public List<AreaSimpleDTO> Areas { get; set; } = new();
        public List<ProjectSimpleDTO> Projects { get; set; } = new();
        public List<LessonPeriodDTO> Periods { get; set; } = new();
        public List<UserFilterDTO> Users { get; set; } = new();
        /// <summary>
        /// Revisores asignados (worker_lesson_jefe_id) a los autores de las
        /// lecciones existentes. Opciones del filtro "Revisor" del listado.
        /// </summary>
        public List<LessonReviewerFilterDTO> Reviewers { get; set; } = new();
        /// <summary>
        /// Filtros dinámicos por catalog_type: una entrada por cada catalog_type
        /// que tenga al menos un catalog_item presente en scope_item activo, con
        /// los items disponibles dentro de ese tipo. El frontend renderiza un
        /// dropdown por entrada.
        /// </summary>
        public List<CatalogFilterGroupDTO> Categories { get; set; } = new();
    }

    public class CatalogFilterGroupDTO
    {
        public int CatalogTypeId { get; set; }
        public string CatalogTypeName { get; set; } = string.Empty;
        public List<CatalogFilterItemDTO> Items { get; set; } = new();
    }

    public class CatalogFilterItemDTO
    {
        public int CatalogItemId { get; set; }
        public string CatalogItemDescription { get; set; } = string.Empty;
    }

    /// <summary>Opción del filtro "Revisor": el workerId del revisor y su nombre.</summary>
    public class LessonReviewerFilterDTO
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
    }
}
