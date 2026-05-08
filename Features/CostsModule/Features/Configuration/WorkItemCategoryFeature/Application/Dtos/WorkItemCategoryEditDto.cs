namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Application.Dtos
{
    public class WorkItemCategoryClauseUpsertDto
    {
        public int?   WorkItemCategoryClauseId { get; set; }
        public string ClauseText              { get; set; } = null!;
        public int    SortOrder               { get; set; }
    }

    public class WorkItemCategoryEditDto
    {
        public int WorkItemCategoryId { get; set; }
        public string WorkItemCategoryDescription { get; set; } = null!;
        public bool Active { get; set; }
        public List<WorkItemCategoryClauseUpsertDto> Clauses { get; set; } = [];
    }
}
