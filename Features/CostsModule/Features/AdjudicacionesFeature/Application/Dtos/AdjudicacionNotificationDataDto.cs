namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    public class AdjudicacionNotificationDataDto
    {
        public int ProjectSubContractorId { get; set; }
        public int ProjectSubContractorStatusId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string WorkItemDescription { get; set; } = null!;
        public string ContributorName { get; set; } = null!;
        public string ContractorEmail { get; set; } = null!;
        public List<string> StaffEmails { get; set; } = new();
        public List<ProjectSubContractorFileDto> QuotationFiles { get; set; } = new();
        public List<ProjectSubContractorFileDto> ComparativeFiles { get; set; } = new();
    }
}
