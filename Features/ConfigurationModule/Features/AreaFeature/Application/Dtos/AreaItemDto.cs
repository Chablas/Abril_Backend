namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Dtos
{
    public class AreaItemDto
    {
        public int AreaItemId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
        public int? AreaItemParentId { get; set; }
        public string? AreaItemParentName { get; set; }
        public bool Active { get; set; }
    }

    public class AreaItemCreateDto
    {
        public string AreaItemName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public int? AreaItemParentId { get; set; }
        public bool Active { get; set; } = true;
    }

    public class AreaItemEditDto
    {
        public int AreaItemId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public int? AreaItemParentId { get; set; }
        public bool Active { get; set; }
    }

    public class AreaItemSimpleDto
    {
        public int AreaItemId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public int? AreaItemParentId { get; set; }
    }

    public class AreaItemFilterDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int? AreaTypeId { get; set; }
        public int? AreaItemParentId { get; set; }
        public bool? Active { get; set; }
        public string? Search { get; set; }
    }

    public class AreaItemTreeDto
    {
        public int AreaItemId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
        public int? AreaItemParentId { get; set; }
        public bool Active { get; set; }
        public List<AreaItemTreeDto> Children { get; set; } = new();
    }
}
