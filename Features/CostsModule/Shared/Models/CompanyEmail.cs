using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.CostsModule.Shared.Models {
    public class CompanyEmail {
        public int CompanyEmailId { get; set; }
        public int CompanyId { get; set; }
        [Column("CompanyEmail")]
        public string Email { get; set; }
        public DateTimeOffset CreatedDateTime { get; set; }
        public int? CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
        public Company Company { get; set; }
    }
}
