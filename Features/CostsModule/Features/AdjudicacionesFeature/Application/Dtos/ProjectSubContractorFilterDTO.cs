namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos {
    public class ProjectSubContractorFilterDTO {
        public int? ProjectId { get; set; }
        public string? ContributorName { get; set; }
        public string? ContributorRuc { get; set; }
        public int? CreatedUserId { get; set; }
        public int Page { get; set; } = 1;
    }
}
