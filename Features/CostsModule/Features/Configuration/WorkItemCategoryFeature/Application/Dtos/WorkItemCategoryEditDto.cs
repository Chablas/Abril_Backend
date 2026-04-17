namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Application.Dtos
{
    public class WorkItemCategoryEditDto
    {
        public int WorkItemCategoryId { get; set; }
        public string WorkItemCategoryDescription { get; set; } = null!;
        public bool Active { get; set; }
    }
}
