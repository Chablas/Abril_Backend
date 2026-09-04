namespace Abril_Backend.Features.Costs.Adjudicaciones.Infrastructure.Models
{
    public class ProjectSubContractorToleranceChart : IAdjudicacionDocument
    {
        public int ProjectSubContractorToleranceChartId { get; set; }
        // Adjudicación a la que pertenece el documento. Se guarda acá (además de la FK que
        // apunta al documento vigente desde project_sub_contractor) para que al eliminarlo
        // (state = false) la fila siga sabiendo de qué adjudicación era.
        public int? ProjectSubContractorId { get; set; }
        public string? FileUrl { get; set; }
        public string? OriginalFileName { get; set; }
        public string? SharepointItemId { get; set; }
        public int? ProjectSubContractorFileStatusId { get; set; }
        public string? Observation { get; set; }
        public DateTimeOffset CreatedDatetime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTimeOffset? UpdatedDatetime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
        public bool State { get; set; }
        public ProjectSubContractorFileStatus? FileStatus { get; set; }
    }
}
