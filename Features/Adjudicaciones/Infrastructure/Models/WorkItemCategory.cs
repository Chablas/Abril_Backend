namespace Abril_Backend.Features.Adjudicaciones.Infrastructure.Models {
    public class WorkItemCategory {
        public int WorkItemCategoryId {get; set;}
        public string WorkItemCategoryDescription {get; set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}