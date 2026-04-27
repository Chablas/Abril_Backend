using Abril_Backend.Features.CostsModule.Shared.Models;

namespace Abril_Backend.Infrastructure.Models {
    public class Project {
        public int ProjectId {get; set;}
        public string ProjectDescription {get; set;}
        public string? LevelDescription {get; set;}
        public int? ContributorId {get; set;}
        public Contributor? Contributor {get; set;}
        public string? ProjectDistrict {get; set;}
        public string? ProjectProvince {get; set;}
        public string? ProjectDepartment {get; set;}
        public string? ProjectLocation {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
        public List<ResidentReportIncidence> Incidences { get; set; }
    }
}
