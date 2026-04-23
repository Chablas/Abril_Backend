namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    public class ScNotificationDataDto
    {
        public string ProjectDescription { get; set; } = null!;
        public string WorkItemDescription { get; set; } = null!;
        public List<string> ContractorEmails { get; set; } = new();
    }
}
