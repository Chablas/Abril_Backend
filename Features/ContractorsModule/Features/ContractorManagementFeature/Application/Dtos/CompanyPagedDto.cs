namespace Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos
{
    public class CompanyPagedDto
    {
        public int CompanyId { get; set; }
        public string CompanyRuc { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string CompanyAddress { get; set; } = null!;
        public string CompanyEconomicActivityDescription { get; set; } = null!;
        public int CompanyStateId { get; set; }
        public string CompanyStateDescription { get; set; } = null!;
        public string? BrochureFileUrl { get; set; }
        public string? FichaRucFileUrl { get; set; }
        public string? ReferencesListFileUrl { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public List<string> Emails { get; set; } = new();
    }
}
