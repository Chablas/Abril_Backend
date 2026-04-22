namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos {

    public class ProjectSubContractorFileDto
    {
        public string FileUrl { get; set; } = null!;
        public string? OriginalFileName { get; set; }
        public int? StatusId { get; set; }
        public string? StatusDescription { get; set; }
        public string? Observation { get; set; }
    }

    public class ProjectSubContractorDTO {
        public int ProjectSubContractorId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; }
        public int ContractorId { get; set; }
        public int ContributorId { get; set; }
        public string ContributorName { get; set; }
        public int ContractId { get; set; }
        public string ContractDescription { get; set; }
        public int ContractTypeId { get; set; }
        public string ContractTypeDescription { get; set; }
        public int ContractOriginId { get; set; }
        public string ContractOriginDescription { get; set; }
        public int PaymentMethodId { get; set; }
        public string PaymentMethodDescription { get; set; }
        public decimal? AdvancePercentage {get;set;}
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; }
        public bool AmountHasIgv { get; set; }
        public string ContractorEmail { get; set; }
        public int WorkItemId { get; set; }
        public string WorkItemDescription { get; set; }
        public int WorkItemCategoryId { get; set; }
        public string WorkItemCategoryDescription { get; set; }
        public int ProjectSubContractorStatusId { get; set; }
        public string ProjectSubContractorStatusDescription { get; set; }
        public DateOnly? SigningDate { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public List<ProjectSubContractorFileDto> QuotationFiles { get; set; } = new();
        public List<ProjectSubContractorFileDto> ComparativeFiles { get; set; } = new();
        // Documentos del contrato (paso 3)
        public ProjectSubContractorFileDto? Contract { get; set; }
        public ProjectSubContractorFileDto? SummarySheet { get; set; }
        public ProjectSubContractorFileDto? Budget { get; set; }
        public ProjectSubContractorFileDto? Schedule { get; set; }
        public ProjectSubContractorFileDto? AttachedQuotation { get; set; }
        public ProjectSubContractorFileDto? ServiceOrder { get; set; }
        public ProjectSubContractorFileDto? PromissoryNote { get; set; }
    }
}