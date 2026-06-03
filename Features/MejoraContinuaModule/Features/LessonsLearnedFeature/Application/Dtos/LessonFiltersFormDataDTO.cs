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
}
