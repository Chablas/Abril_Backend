namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos {
    /// <summary>
    /// Campos del paso 1 (información de la adjudicación) editables mientras la
    /// adjudicación esté en pasos 1–4. Equivale a los datos de creación, sin archivos.
    /// </summary>
    public class ProjectSubContractorUpdateInfoDTO {
        public int ProjectId { get; set; }
        public int ContractorId { get; set; }
        public int ContractTypeId { get; set; }
        public int? ContractModalityId { get; set; }
        public int PaymentMethodId { get; set; }
        public int? PaymentFormId { get; set; }
        public bool IncludesCartaFianza { get; set; }
        public decimal AdvancePercentage { get; set; }
        public decimal? AdvanceAmount { get; set; }
        public decimal Amount { get; set; }
        public int CurrencyId { get; set; }
        public bool HasIgv { get; set; }
        public int WorkItemId { get; set; }
        public int WorkItemCategoryId { get; set; }
        public int? WorkSpecialtyId { get; set; }
    }
}
