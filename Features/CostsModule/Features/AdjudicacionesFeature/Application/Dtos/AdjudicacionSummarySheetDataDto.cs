namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    public class AdjudicacionSummarySheetDataDto
    {
        public int ProjectSubContractorId { get; set; }
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string? Abbreviation { get; set; }
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
        public string? ProjectLocation { get; set; }
        /// <summary>N° de niveles: project.level_description, con fallback a project.num_niveles.</summary>
        public string? Niveles { get; set; }
        public string? ProjectLegalEntityRegistryNumber { get; set; }
        public string WorkItemDescription { get; set; } = null!;
        public string ContractTypeDescription { get; set; } = null!;
        public int? ContractModalityId { get; set; }
        public int PaymentMethodId { get; set; }
        public string PaymentMethodDescription { get; set; } = null!;
        public string? PaymentFormDescription { get; set; }
        public bool IncludesCartaFianza { get; set; }
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
        public int? GuaranteeFundPercentage { get; set; }
        public int? GuaranteeFundDays { get; set; }
        public int? GuaranteeValidityDays { get; set; }
        /// <summary>Forma de pago en días hábiles (paso 2) — "pago a x días hábiles".</summary>
        public int PaymentDays { get; set; }

        /// <summary>Cláusulas especiales de la partida de control (9.x / 7.x), ordenadas por SortOrder.</summary>
        public List<string> SpecialClauses { get; set; } = [];

        /// <summary>Cláusulas de Anexo 3 (Suministro) de la partida de control, ordenadas por SortOrder.</summary>
        public List<string> SpecialClausesAnexo3 { get; set; } = [];

        /// <summary>Cláusulas de Anexo 4 (Suministro) de la partida de control, ordenadas por SortOrder.</summary>
        public List<string> SpecialClausesAnexo4 { get; set; } = [];
    }
}
