namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos {
    public class ProjectSubContractorCreateDTO {
        public int ProjectId { get; set; }
        public int ContractorId { get; set; }
        public int ContractId { get; set; }
        public int ContractTypeId { get; set; }
        public int? ContractModalityId { get; set; }
        public int ContractOriginId { get; set; }
        public int PaymentMethodId { get; set; }
        public decimal AdvancePercentage { get; set; }
        public decimal? AdvanceAmount { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public bool HasIgv { get; set; }
        public int WorkItemId { get; set; }
        public int WorkItemCategoryId { get; set; }
        public List<IFormFile>? QuotationFiles { get; set; }
        public List<IFormFile>? ComparativeFiles { get; set; }
    }
}