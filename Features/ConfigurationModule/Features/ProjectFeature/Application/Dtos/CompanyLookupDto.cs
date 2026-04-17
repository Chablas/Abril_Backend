namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    public class CompanyLookupDto
    {
        public int CompanyId { get; set; }
        public string CompanyRuc { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string CompanyAddress { get; set; } = null!;
    }
}
