namespace Abril_Backend.Features.CostsModule.Shared.Models
{
    public class WorkItemCategory
    {
        public int WorkItemCategoryId { get; set; }
        public string WorkItemCategoryDescription { get; set; } = null!;
        public DateTimeOffset CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
