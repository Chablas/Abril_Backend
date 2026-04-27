namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    public class ProjectEditDto
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = null!;
        public string? LevelDescription { get; set; }
        public int? ContributorId { get; set; }
        public string? ProjectDistrict { get; set; }
        public string? ProjectProvince { get; set; }
        public string? ProjectDepartment { get; set; }
        public string? ProjectLocation { get; set; }
        public bool Active { get; set; }
    }
}
