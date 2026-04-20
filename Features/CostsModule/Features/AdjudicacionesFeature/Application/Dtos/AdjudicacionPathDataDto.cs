namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    /// <summary>Datos mínimos para construir la ruta de carpetas en SharePoint.</summary>
    public class AdjudicacionPathDataDto
    {
        public int    ProjectSubContractorId { get; set; }
        public string ProjectDescription    { get; set; } = null!;
        public string CompanyRuc            { get; set; } = null!;
        public string CompanyName           { get; set; } = null!;
        public string WorkItemDescription   { get; set; } = null!;
    }
}
