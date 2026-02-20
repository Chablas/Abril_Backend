using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Infrastructure.Models {
    public class Phase {
        public int PhaseId {get; set;}
        public string? PhaseDescription {get; set;}
        [Column("phase_order")]
        public int? Order {get;set;}
        public DateTime CreatedDateTime {get; set;}
        public int CreatedUserId {get; set;}
        public DateTime? UpdatedDateTime {get; set;}
        public int? UpdatedUserId {get; set;}
        public bool Active {get; set;}
        public bool State {get; set;}
    }
}