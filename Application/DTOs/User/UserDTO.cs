namespace Abril_Backend.Application.DTOs {
    public class UserDTO {
        public int UserId {get; set;}
        public PersonDTO Person {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
    }
}