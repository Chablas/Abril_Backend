namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    public class ProjectCreateDto
    {
        public string ProjectDescription { get; set; } = null!;
        public string? LevelDescription { get; set; }
        public int? ContributorId { get; set; }
        public string? District { get; set; }
        public string? Location { get; set; }
        public bool Active { get; set; }
    }
}
