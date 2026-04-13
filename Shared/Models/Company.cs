namespace Abril_Backend.Shared.Models
{
    public class Company
    {
        public int CompanyId { get; set; }
        public string CompanyRuc { get; set; }
        public string CompanyName { get; set; }
        public string CompanyAddress { get; set; }
        public string CompanyEconomicActivityDescription { get; set; }
        public int CompanyStateId { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
        public List<CompanyEmail> Emails { get; set; } = new();
    }
}
