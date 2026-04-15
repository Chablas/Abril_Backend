namespace Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos {
    public class CompanyCreateDto {
        public string CompanyRuc { get; set; }
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }
        public string CompanyEconomicActivityDescription { get; set; }
        public IFormFile? BrochureFile { get; set; }
        public IFormFile? FichaRucFile { get; set; }
        public IFormFile? ReferencesListFile { get; set; }
        public List<string> CompanyEmails { get; set; }
        public string? GraphAccessToken { get; set; }
    }
}
