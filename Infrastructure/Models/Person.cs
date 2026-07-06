namespace Abril_Backend.Infrastructure.Models {
    public class Person {
        public int PersonId {get; set;}
        public int? UserId {get;set;}
        public int? DocumentIdentityTypeId {get; set;}
        public string? DocumentIdentityCode {get; set;}
        public string? FirstNames {get; set;}
        public string? FirstName {get; set;}
        public string? SecondName {get; set;}
        public string? FirstLastName {get; set;}
        public string? SecondLastName {get; set;}
        public string? FullName {get; set;}
        public string? Sexo {get; set;}
        public int? PhoneNumber {get;set;}
        /// <summary>
        /// Fecha de cumpleaños del trabajador (columna <c>cumpleanos</c>). Solo interesa
        /// el día y mes para el calendario del boletín, pero se guarda fecha completa.
        /// </summary>
        public DateOnly? Cumpleanos {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int? CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
        /// <summary>Bytes de la firma (PNG) dibujada por esta persona en Configuración.</summary>
        public byte[]? SignatureImageBytes {get; set;}
        public string? SignatureMime {get; set;}
        public DateTimeOffset? SignatureUpdatedDateTime {get; set;}
        public User User { get; set; }
    }
}