namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Application.Dtos
{
    public class RelationFlatDTO
    {
        public int LinkId { get; set; }

        public int PhaseId { get; set; }
        public string PhaseDescription { get; set; } = string.Empty;

        public int? StageId { get; set; }
        public string? StageDescription { get; set; }

        public int? LayerId { get; set; }
        public string? LayerDescription { get; set; }

        public int? SubStageId { get; set; }
        public string? SubStageDescription { get; set; }

        public int? SubSpecialtyId { get; set; }
        public string? SubSpecialtyDescription { get; set; }

        public int? PartidaId { get; set; }
        public string? PartidaDescription { get; set; }
    }
}
