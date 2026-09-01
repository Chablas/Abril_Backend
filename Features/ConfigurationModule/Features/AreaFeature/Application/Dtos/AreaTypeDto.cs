namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Dtos
{
    public class AreaTypeDto
    {
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    public class AreaTypeCreateDto
    {
        public string AreaTypeName { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
    }

    public class AreaTypeEditDto
    {
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    public class AreaTypeSimpleDto
    {
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
    }

    /// <summary>Filtros del listado paginado. Mismo juego que AreaItemFilterDto, sin el tipo.</summary>
    public class AreaTypeFilterDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public bool? Active { get; set; }
        public string? Search { get; set; }
    }
}
