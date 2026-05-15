namespace Abril_Backend.Infrastructure.Models
{
    public class ScopeTemplateItem
    {
        public int ScopeTemplateItemId { get; set; }
        public int ScopeTemplateId { get; set; }
        /// <summary>Referencia directa al ítem de catálogo — independiente del área/subárea.</summary>
        public int CatalogItemId { get; set; }
        public bool Active { get; set; }

        public ScopeTemplate ScopeTemplate { get; set; } = null!;
        public CatalogItem CatalogItem { get; set; } = null!;
    }
}
