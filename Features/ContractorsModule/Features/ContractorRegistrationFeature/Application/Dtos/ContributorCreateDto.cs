namespace Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos {
    public class ContributorCreateDto {
        public string ContributorRuc { get; set; }
        public string ContributorName { get; set; }
        public string ContributorAddress { get; set; }
        public string ContributorEconomicActivityDescription { get; set; }
        public string? ContributorDistrict { get; set; }
        public string? ContributorProvince { get; set; }
        public string? ContributorDepartment { get; set; }
        public string? LegalRepresentativeDni { get; set; }
        public string? LegalRepresentativeFullName { get; set; }
        public string? LegalEntityRegistryNumber { get; set; }
        public IFormFile? LogoFile { get; set; }
        public IFormFile? BrochureFile { get; set; }
        public IFormFile? FichaRucFile { get; set; }
        public IFormFile? ReferencesListFile { get; set; }
        /// <summary>Lista de correos. En multipart/form-data se envía como campos repetidos: ContributorEmails=a&amp;ContributorEmails=b</summary>
        public List<string> ContributorEmails { get; set; } = [];
        /// <summary>Clasificaciones paralelas a ContributorEmails (mismo orden). Valor vacío = sin clasificación.</summary>
        public List<string?> ContributorEmailPersonTypeIds { get; set; } = [];
        public string? GraphAccessToken { get; set; }
    }
}
