namespace Abril_Backend.Application.DTOs {
    public class UserProjectDTO {
        public int UserProjectId {get; set;}
        public int UserId {get; set;}
        public string? UserFullName {get;set;}
        public int ProjectId { get; set; }
        public string? ProjectDescription { get; set; }
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
    }
}