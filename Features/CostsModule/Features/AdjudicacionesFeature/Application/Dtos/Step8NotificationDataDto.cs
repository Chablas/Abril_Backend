namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    public class Step8NotificationDataDto
    {
        public string ProjectDescription  { get; set; } = null!;
        public string ContractDescription { get; set; } = null!;
        public string ContributorName     { get; set; } = null!;
        public List<string> OfTecnicaEmails { get; set; } = new();
        public List<ProjectSubContractorFileDto> ScannedDocs { get; set; } = new();
    }
}
