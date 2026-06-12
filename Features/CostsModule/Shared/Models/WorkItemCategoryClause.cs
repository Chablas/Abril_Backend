namespace Abril_Backend.Features.CostsModule.Shared.Models
{
    public class WorkItemCategoryClause
    {
        public int    WorkItemCategoryClauseId { get; set; }
        public int    WorkItemCategoryId       { get; set; }
        public string ClauseText              { get; set; } = null!;
        public int    SortOrder               { get; set; }
        public bool   State                   { get; set; }

        // Tipos de contrato a los que aplica la cláusula (no se triplica el texto: una fila, 3 flags)
        public bool   AppliesSuministro             { get; set; } = true;
        public bool   AppliesInstalacion            { get; set; } = true;
        public bool   AppliesSuministroInstalacion  { get; set; } = true;

        public DateTimeOffset  CreatedDatetime  { get; set; }
        public int             CreatedUserId    { get; set; }
        public DateTimeOffset? UpdatedDatetime  { get; set; }
        public int?            UpdatedUserId    { get; set; }

        // Navegación
        public WorkItemCategory WorkItemCategory { get; set; } = null!;
    }
}
