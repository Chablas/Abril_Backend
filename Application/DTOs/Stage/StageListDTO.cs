namespace Abril_Backend.Application.DTOs {
    public class StageDTO {
        public int StageId {get; set;}
        public string StageDescription {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
    }
}