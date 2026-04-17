namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Application.Dtos
{
    public class WorkItemCategoryDto
    {
        public int WorkItemCategoryId { get; set; }
        public string WorkItemCategoryDescription { get; set; } = null!;
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
    }
}
