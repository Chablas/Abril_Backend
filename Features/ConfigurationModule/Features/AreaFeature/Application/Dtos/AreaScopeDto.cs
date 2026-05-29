namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Dtos
{
    public class AreaScopeTreeDto
    {
        public int AreaScopeId { get; set; }
        public int AreaItemId { get; set; }
        public string AreaItemName { get; set; } = string.Empty;
        public int AreaTypeId { get; set; }
        public string AreaTypeName { get; set; } = string.Empty;
        public int? AreaScopeParentId { get; set; }
        public int DisplayOrder { get; set; }
        public bool Active { get; set; }
        public List<AreaScopeTreeDto> Children { get; set; } = new();
    }

    /// <summary>Nodo dentro de una nueva rama. tempId es un identificador local del cliente.</summary>
    public class AreaScopeBranchNodeDto
    {
        public int TempId { get; set; }
        public int AreaItemId { get; set; }
        public int? ParentTempId { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class AreaScopeBranchDto
    {
        public List<AreaScopeBranchNodeDto> Nodes { get; set; } = new();
    }
}
