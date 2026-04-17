namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    public class ProjectDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string? LevelDescription { get; set; }
        public int? CompanyId { get; set; }
        public string? CompanyRuc { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyAddress { get; set; }
        public string? District { get; set; }
        public string? Location { get; set; }
        public bool Active { get; set; }
    }
}
