namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.CatalogFeature.Application.Dtos
{
    public class CatalogItemDTO
    {
        public int CatalogItemId { get; set; }
        public int CatalogTypeId { get; set; }
        public string CatalogTypeName { get; set; } = string.Empty;
        public string CatalogTypeCode { get; set; } = string.Empty;
        public int? CatalogItemParentId { get; set; }
        public string? ParentDescription { get; set; }
        public string CatalogItemDescription { get; set; } = string.Empty;
        public string? CatalogItemCode { get; set; }
        public bool Active { get; set; }
        public List<CatalogItemDTO> Children { get; set; } = new();
    }

    public class CatalogItemCreateDTO
    {
        public int CatalogTypeId { get; set; }
        public int? CatalogItemParentId { get; set; }
        public string CatalogItemDescription { get; set; } = string.Empty;
        public string? CatalogItemCode { get; set; }
        public bool Active { get; set; } = true;
    }

    public class CatalogItemEditDTO
    {
        public int CatalogItemId { get; set; }
        public int CatalogTypeId { get; set; }
        public int? CatalogItemParentId { get; set; }
        public string CatalogItemDescription { get; set; } = string.Empty;
        public string? CatalogItemCode { get; set; }
        public bool Active { get; set; }
    }
}
