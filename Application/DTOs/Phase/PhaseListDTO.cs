namespace Abril_Backend.Application.DTOs {
    public class PhaseDTO {
        public int PhaseId {get; set;}
        public string PhaseDescription {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
    }
}