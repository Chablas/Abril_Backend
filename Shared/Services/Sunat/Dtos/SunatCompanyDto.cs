namespace Abril_Backend.Shared.Services.Sunat.Dtos
{
    public class SunatCompanyDto
    {
        public string CompanyRuc { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string CompanyAddress { get; set; } = null!;
        public string CompanyEconomicActivityDescription { get; set; } = null!;
    }
}
