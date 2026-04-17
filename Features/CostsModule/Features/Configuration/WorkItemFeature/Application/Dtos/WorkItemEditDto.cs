namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos
{
    public class WorkItemEditDto
    {
        public int WorkItemId { get; set; }
        public string WorkItemDescription { get; set; } = null!;
        public bool Active { get; set; }
    }
}
