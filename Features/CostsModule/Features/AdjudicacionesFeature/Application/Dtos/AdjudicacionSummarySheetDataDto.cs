namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    public class AdjudicacionSummarySheetDataDto
    {
        public int ProjectSubContractorId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string ContributorName { get; set; } = null!;
        public string ContributorRuc { get; set; } = null!;
        public string? ContributorAddress { get; set; }
        public string? ContributorDistrict { get; set; }
        public string? ContributorProvince { get; set; }
        public string? ContributorDepartment { get; set; }
        public string? LegalRepresentativeFullName { get; set; }
        public string? LegalRepresentativeDni { get; set; }
        public string? LegalEntityRegistryNumber { get; set; }
        public string? ProjectRazonSocial { get; set; }
        public string? ProjectContributorRuc { get; set; }
        public string? ProjectDistrict { get; set; }
        public string? ProjectLegalEntityRegistryNumber { get; set; }
        public string WorkItemDescription { get; set; } = null!;
        public string ContractDescription { get; set; } = null!;
        public string ContractTypeDescription { get; set; } = null!;
        public string ContractOriginDescription { get; set; } = null!;
        public string PaymentMethodDescription { get; set; } = null!;
        public string CurrencyCode { get; set; } = null!;
        public decimal Amount { get; set; }
        public bool HasIgv { get; set; }
        public decimal? AdvancePercentage { get; set; }
        public decimal? AdvanceAmount { get; set; }
        public int? TermDays { get; set; }
        public DateOnly? SigningDate { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? ContractNumber { get; set; }
        public int? PromissoryNoteNumber { get; set; }
    }
}
