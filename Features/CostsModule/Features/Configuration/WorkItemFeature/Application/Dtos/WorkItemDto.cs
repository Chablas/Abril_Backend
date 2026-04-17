namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos
{
    public class WorkItemDto
    {
        public int WorkItemId { get; set; }
        public string WorkItemDescription { get; set; } = null!;
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
    }
}
