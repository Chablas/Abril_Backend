namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    public class AdjudicacionSummarySheetDataDto
    {
        public int ProjectSubContractorId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string ContributorName { get; set; } = null!;
        public string ContributorRuc { get; set; } = null!;
        public string WorkItemDescription { get; set; } = null!;
        public string ContractDescription { get; set; } = null!;
        public string ContractTypeDescription { get; set; } = null!;
        public string ContractOriginDescription { get; set; } = null!;
        public string PaymentMethodDescription { get; set; } = null!;
        public string CurrencyCode { get; set; } = null!;
        public decimal Amount { get; set; }
        public bool HasIgv { get; set; }
        public decimal? AdvancePercentage { get; set; }
        public DateOnly? SigningDate { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }
}
