namespace Abril_Backend.Features.Adjudicaciones.Application.Dtos {
    public class ProjectSubContractorDTO {
        public int ProjectSubContractorId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; }
        public int ContractId { get; set; }
        public string ContractDescription { get; set; }
        public int ContractTypeId { get; set; }
        public string ContractTypeDescription { get; set; }
        public int ContractOriginId { get; set; }
        public string ContractOriginDescription { get; set; }
        public int PaymentMethodId { get; set; }
        public string PaymentMethodDescription { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; }
        public bool AmountHasIgv { get; set; }
        public string ContractorEmail { get; set; }
        public int WorkItemId { get; set; }
        public string WorkItemDescription { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public List<string> QuotationFileUrls { get; set; } = new();
        public List<string> ComparativeFileUrls { get; set; } = new();
    }
}