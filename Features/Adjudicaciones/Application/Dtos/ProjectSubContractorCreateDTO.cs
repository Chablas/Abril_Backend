namespace Abril_Backend.Features.Adjudicaciones.Application.Dtos {
    public class ProjectSubContractorCreateDTO {
        public int ProjectId { get; set; }
        public int CompanyId { get; set; }
        public int ContractId { get; set; }
        public int ContractTypeId { get; set; }
        public int ContractOriginId { get; set; }
        public int PaymentMethodId { get; set; }
        public decimal AdvancePercentage { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public bool HasIgv { get; set; }
        public string ContractorEmail { get; set; }
        public int WorkItemId { get; set; }
        public List<IFormFile>? QuotationFiles { get; set; }
        public List<IFormFile>? ComparativeFiles { get; set; }
    }
}