namespace Abril_Backend.Features.Adjudicaciones.Infrastructure.Models {
    public class ProjectSubContractorComparativeFile {
        public int ProjectSubContractorComparativeFileId {get; set;}
        public int ProjectSubContractorId {get; set;}
        public string FileUrl {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
        public ProjectSubContractor ProjectSubContractor { get; set; }
    }
}