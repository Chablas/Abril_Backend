namespace Abril_Backend.Features.CostsModule.Shared.Models {
    public class CompanyState {
        public int CompanyStateId { get; set; }
        public string CompanyStateDescription { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
    }
}
