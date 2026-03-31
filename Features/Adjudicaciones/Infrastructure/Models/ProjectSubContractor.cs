using Abril_Backend.Infrastructure.Models;
namespace Abril_Backend.Features.Adjudicaciones.Infrastructure.Models {
    public class ProjectSubContractor {
        public int ProjectSubContractorId {get; set;}
        public int ProjectId {get; set;}
        public int CompanyId {get; set;}
        public int ContractId {get; set;}
        public int ContractTypeId {get; set;}
        public int ContractOriginId {get; set;}
        public int PaymentMethodId {get; set;}
        public decimal Amount {get; set;}
        public int CurrencyId {get; set;}
        public bool HasIgv {get; set;}
        public string ContractorEmail {get; set;}
        public int WorkItemId {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
        public Project Project { get; set; }
        public List<ProjectSubContractorQuotationFile> QuotationFiles { get; set; } = new();
        public List<ProjectSubContractorComparativeFile> ComparativeFiles { get; set; } = new();
    }
}