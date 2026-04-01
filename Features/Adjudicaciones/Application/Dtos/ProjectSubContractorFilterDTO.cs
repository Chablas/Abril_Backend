namespace Abril_Backend.Features.Adjudicaciones.Application.Dtos {
    public class ProjectSubContractorFilterDTO {
        public int? ProjectId { get; set; }
        public string? CompanyName { get; set; }
        public string? CompanyRuc { get; set; }
        public int? CreatedUserId { get; set; }
        public int Page { get; set; } = 1;
    }
}
