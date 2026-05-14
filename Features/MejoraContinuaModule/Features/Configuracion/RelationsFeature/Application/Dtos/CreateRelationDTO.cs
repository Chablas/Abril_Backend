namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.RelationsFeature.Application.Dtos
{
    public class CreateRelationDTO
    {
        public int PhaseId { get; set; }
        public int StageId { get; set; }
        public int? LayerId { get; set; }
        public int? SubStageId { get; set; }
        public int? SubSpecialtyId { get; set; }
        public int? PartidaId { get; set; }
        public bool Active { get; set; }
    }
}
