namespace Abril_Backend.Features.Contractors.ContractorRegistration.Application.Dtos {
    public class CompanyCreateDto {
        public string CompanyRuc { get; set; }
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }
        public string CompanyEconomicActivityDescription { get; set; }
        public List<string> CompanyEmails { get; set; }
    }
}
