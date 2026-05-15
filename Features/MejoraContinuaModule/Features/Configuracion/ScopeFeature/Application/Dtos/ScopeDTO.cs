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
        public string CatalogTypeCode { get; set; } = string.Empty;
        public int? ScopeItemParentId { get; set; }
        public int DisplayOrder { get; set; }
        public bool Active { get; set; }
        public List<ScopeItemDTO> Children { get; set; } = new();
    }

    public class ScopeItemUpsertDTO
    {
        /// <summary>Id del area_subarea al que pertenece este scope.</summary>
        public int AreaSubareaId { get; set; }
        /// <summary>Lista completa de scope items que deben quedar (reemplaza lo existente).</summary>
        public List<ScopeItemNodeDTO> Items { get; set; } = new();
    }

    public class ScopeItemNodeDTO
    {
        public int CatalogItemId { get; set; }
        public int? ParentCatalogItemId { get; set; }
        public int DisplayOrder { get; set; }
    }

    // ── ScopeTemplate ────────────────────────────────────────────
    /// <summary>Plantilla global (no está ligada a ningún área/subárea específica).</summary>
    public class ScopeTemplateDTO
    {
        public int ScopeTemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public bool Active { get; set; }
        /// <summary>IDs de catalog_item que forman la plantilla.</summary>
        public List<int> CatalogItemIds { get; set; } = new();
    }

    public class ScopeTemplateCreateDTO
    {
        public string TemplateName { get; set; } = string.Empty;
        public List<int> CatalogItemIds { get; set; } = new();
    }

    public class ScopeTemplateUpdateDTO
    {
        public int ScopeTemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;
        public List<int> CatalogItemIds { get; set; } = new();
    }
}
