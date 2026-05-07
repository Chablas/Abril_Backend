using Abril_Backend.Infrastructure.Models;
using Abril_Backend.Features.CostsModule.Shared.Models;
using Abril_Backend.Shared.Models;
namespace Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Models {
    public class ProjectSubContractor {
        public int ProjectSubContractorId {get; set;}
        public int ProjectId {get; set;}
        public int ContractorId {get; set;}
        public int ContractId {get; set;}
        public int ContractTypeId {get; set;}
        public int ContractOriginId {get; set;}
        public int PaymentMethodId {get; set;}
        public decimal AdvancePercentage {get;set;}
        public decimal? AdvanceAmount {get; set;}
        public decimal Amount {get; set;}
        public int CurrencyId {get; set;}
        public bool HasIgv {get; set;}
        public string ContractorEmail {get; set;}
        public int WorkItemId {get; set;}
        public int WorkItemCategoryId {get; set;}
        public int ProjectSubContractorStatusId {get;set;}

        // Expediente dates (step 2)
        public DateOnly? SigningDate { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public int? TermDays { get; set; }

        // Número de contrato y pagaré (paso 2)
        public int? ContractNumber { get; set; }
        public int? PromissoryNoteNumber { get; set; }

        // Llegada a Of. Central (paso 5)
        public bool? ArrivedWithObservations { get; set; }

        // Document FKs (nullable — populated as the expediente progresses)
        public int? ProjectSubContractorContractId { get; set; }
        public int? ProjectSubContractorSummarySheetId { get; set; }
        public int? ProjectSubContractorBudgetId { get; set; }
        public int? ProjectSubContractorScheduleId { get; set; }
        public int? ProjectSubContractorAttachedQuotationId { get; set; }
        public int? ProjectSubContractorServiceOrderId { get; set; }
        public int? ProjectSubContractorPromissoryNoteId { get; set; }
        public int? ProjectSubContractorPackageId { get; set; }
        public int? ProjectSubContractorInstructivoId { get; set; }

        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
        public Project Project { get; set; }
        public Contractor Contractor { get; set; } = null!;
        public ProjectSubContractorContract? Contract { get; set; }
        public ProjectSubContractorSummarySheet? SummarySheet { get; set; }
        public ProjectSubContractorBudget? Budget { get; set; }
        public ProjectSubContractorSchedule? Schedule { get; set; }
        public ProjectSubContractorAttachedQuotation? AttachedQuotation { get; set; }
        public ProjectSubContractorServiceOrder? ServiceOrder { get; set; }
        public ProjectSubContractorPromissoryNote? PromissoryNote { get; set; }
        public ProjectSubContractorPackage? Package { get; set; }
        public ProjectSubContractorInstructivo? Instructivo { get; set; }
        public List<ProjectSubContractorQuotationFile> QuotationFiles { get; set; } = new();
        public List<ProjectSubContractorComparativeFile> ComparativeFiles { get; set; } = new();
    }
}