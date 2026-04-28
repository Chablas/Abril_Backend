namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    public class Step3ApprovalDataDto
    {
        public string ProjectDescription  { get; set; } = null!;
        public string ContributorName     { get; set; } = null!;
        public string WorkItemDescription { get; set; } = null!;
        public List<string> OfTecnicaEmails { get; set; } = new();
    }
}
