namespace Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos
{
    public class ContractorUpdateDto
    {
        public string ContributorName { get; set; } = null!;
        public string ContributorAddress { get; set; } = null!;
        public string ContributorEconomicActivityDescription { get; set; } = null!;
        public string? ContributorDistrict { get; set; }
        public string? ContributorProvince { get; set; }
        public string? ContributorDepartment { get; set; }
        public string? LegalEntityRegistryNumber { get; set; }
        public string? LegalRepresentativeDni { get; set; }
        public string? LegalRepresentativeFullName { get; set; }
        /// <summary>Lista de correos: se envía como campos repetidos (emails=a&amp;emails=b) en multipart/form-data.</summary>
        public List<string> Emails { get; set; } = [];
        public IFormFile? LogoFile { get; set; }
        public IFormFile? BrochureFile { get; set; }
        public IFormFile? FichaRucFile { get; set; }
        public IFormFile? ReferencesListFile { get; set; }
    }
}
