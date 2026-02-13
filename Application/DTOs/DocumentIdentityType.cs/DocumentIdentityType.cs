namespace Abril_Backend.Application.DTOs {
    public class DocumentIdentityTypeDTO {
        public int DocumentIdentityTypeId { get; set; }
        public string DocumentIdentityTypeDescription { get; set; }
        public string DocumentIdentityTypeAbbreviation { get; set; }
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
    }
}