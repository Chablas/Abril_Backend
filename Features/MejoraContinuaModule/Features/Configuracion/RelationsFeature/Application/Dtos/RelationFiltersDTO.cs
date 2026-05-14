namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Application.Dtos
{
    public class RelationFiltersDTO
    {
        public List<RelationPhaseDTO> Phases { get; set; } = [];
        public List<RelationStageDTO> Stages { get; set; } = [];
        public List<RelationLayerDTO> Layers { get; set; } = [];
        public List<RelationSubStageDTO> SubStages { get; set; } = [];
        public List<RelationSubSpecialtyDTO> SubSpecialties { get; set; } = [];
        public List<PartidaSimpleDTO> Partidas { get; set; } = [];
    }

    public class RelationPhaseDTO
    {
        public int PhaseId { get; set; }
        public string PhaseDescription { get; set; } = string.Empty;
    }

    public class RelationStageDTO
    {
        public int StageId { get; set; }
        public string StageDescription { get; set; } = string.Empty;
    }

    public class RelationLayerDTO
    {
        public int LayerId { get; set; }
        public string LayerDescription { get; set; } = string.Empty;
    }

    public class RelationSubStageDTO
    {
        public int SubStageId { get; set; }
        public string SubStageDescription { get; set; } = string.Empty;
    }

    public class RelationSubSpecialtyDTO
    {
        public int SubSpecialtyId { get; set; }
        public string SubSpecialtyDescription { get; set; } = string.Empty;
    }
}
