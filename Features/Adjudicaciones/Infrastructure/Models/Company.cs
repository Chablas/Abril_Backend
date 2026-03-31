namespace Abril_Backend.Features.Adjudicaciones.Infrastructure.Models {
    public class Company {
        public int CompanyId {get; set;}
        public string CompanyRuc {get; set;}
        public string CompanyName {get; set;}
        public string Address {get; set;}
        public string EconomicActivityDescription {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}