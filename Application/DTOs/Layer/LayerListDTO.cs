namespace Abril_Backend.Application.DTOs {
    public class LayerDTO {
        public int LayerId {get; set;}
        public string LayerDescription {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
    }
}