namespace Abril_Backend.Infrastructure.Models {
    public class Person {
        public int PersonId {get; set;}
        public int? DocumentIdentityTypeId {get; set;}
        public string? DocumentIdentityCode {get; set;}
        public string? FirstNames {get; set;}
        public string? FirstName {get; set;}
        public string? SecondName {get; set;}
        public string? FirstLastName {get; set;}
        public string? SecondLastName {get; set;}
        public string? FullName {get; set;}
        public string? Email {get; set;}
        public int? PhoneNumber {get;set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}