namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos {
    public class CompanyFactoryDTO {
        public int CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyRuc { get; set; }
        public List<string> Emails { get; set; } = new();
    }
}