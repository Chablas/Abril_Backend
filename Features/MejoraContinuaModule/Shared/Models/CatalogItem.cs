namespace Abril_Backend.Infrastructure.Models
{
    public class CatalogItem
    {
        public int CatalogItemId { get; set; }
        public int CatalogTypeId { get; set; }
        public int? CatalogItemParentId { get; set; }
        public string CatalogItemDescription { get; set; } = string.Empty;
        public string? CatalogItemCode { get; set; }
        public bool Active { get; set; }

        public CatalogType CatalogType { get; set; } = null!;
        public CatalogItem? Parent { get; set; }
        public ICollection<CatalogItem> Children { get; set; } = new List<CatalogItem>();
    }
}
