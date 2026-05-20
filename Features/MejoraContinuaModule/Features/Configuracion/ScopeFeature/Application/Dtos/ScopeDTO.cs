namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.ScopeFeature.Application.Dtos
{
    // ── AreaSubarea ─────────────────────────────────────────────
    public class AreaSubareaDTO
    {
        public int AreaSubareaId { get; set; }
        public int AreaId { get; set; }
        public int? SubAreaId { get; set; }
    }

    // ── ScopeItem ────────────────────────────────────────────────
    public class ScopeItemDTO
    {
        public int ScopeItemId { get; set; }
        public int AreaSubareaId { get; set; }
        public int CatalogItemId { get; set; }
        public string CatalogItemDescription { get; set; } = string.Empty;
        public string CatalogTypeName { get; set; } = string.Empty;
        public int? ScopeItemParentId { get; set; }
        public int DisplayOrder { get; set; }
        public bool Active { get; set; }
        public List<ScopeItemDTO> Children { get; set; } = new();
    }

    public class ScopeItemUpsertDTO
    {
        public int AreaSubareaId { get; set; }
        public List<ScopeItemNodeDTO> Items { get; set; } = new();
    }

    public class ScopeItemNodeDTO
    {
        public int CatalogItemId { get; set; }
        public int? ParentCatalogItemId { get; set; }
        public int DisplayOrder { get; set; }
    }

    // ── ScopeTemplate ────────────────────────────────────────────
    public class ScopeTemplateItemNodeDTO
    {
        public int CatalogItemId { get; set; }
        public string CatalogItemDescription { get; set; } = string.Empty;
        public int? ScopeTemplateItemParentId { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ScopeTemplateDTO
    {
        public int ScopeTemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public bool Active { get; set; }
        public List<ScopeTemplateItemNodeDTO> Items { get; set; } = new();
    }

    public class ScopeTemplateCreateDTO
    {
        public string TemplateName { get; set; } = string.Empty;
        public List<ScopeTemplateItemNodeDTO> Items { get; set; } = new();
    }

    public class ScopeTemplateUpdateDTO
    {
        public int ScopeTemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public List<ScopeTemplateItemNodeDTO> Items { get; set; } = new();
    }
}
