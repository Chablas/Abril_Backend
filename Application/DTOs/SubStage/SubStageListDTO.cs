namespace Abril_Backend.Application.DTOs {
    public class SubStageDTO {
        public int SubStageId {get; set;}
        public string SubStageDescription {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
    }
}