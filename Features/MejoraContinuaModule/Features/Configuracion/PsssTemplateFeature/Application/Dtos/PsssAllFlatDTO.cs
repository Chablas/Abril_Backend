namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.PsssTemplateFeature.Application.Dtos
{
    public class PsssAllFlatDTO
    {
        public int PsssId { get; set; }
        public string Label { get; set; } = string.Empty;
        public int PhaseId { get; set; }
        public string PhaseDescription { get; set; } = string.Empty;
        public int? TemplateId { get; set; }
        public string? TemplateName { get; set; }
    }
}
