namespace Abril_Backend.Features.Adjudicaciones.Infrastructure.Models {
    public class Currency {
        public int CurrencyId {get; set;}
        public string CurrencyCode {get; set;}
        public string CurrencyDescription {get; set;}
        public string CurrencySymbol {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}