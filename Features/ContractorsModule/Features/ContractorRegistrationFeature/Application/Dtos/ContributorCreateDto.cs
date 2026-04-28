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
        public IFormFile? BrochureFile { get; set; }
        public IFormFile? FichaRucFile { get; set; }
        public IFormFile? ReferencesListFile { get; set; }
        public List<string> ContributorEmails { get; set; }
        public string? GraphAccessToken { get; set; }
    }
}
