namespace Abril_Backend.Features.Contractors.ContractorManagement.Application.Dtos
{
    public class CompanyFilterDto
    {
        public string? CompanyName { get; set; }
        public string? CompanyRuc { get; set; }
        public int Page { get; set; } = 1;
    }
}
