namespace Abril_Backend.Application.DTOs
{
    public class PhaseStageSubStageSubSpecialtyDTO
    {
        public int PhaseId { get; set; }
        public string PhaseDescription { get; set; }
        public int? LinkId { get; set; }
        public List<StageFilterDTO>? Stages { get; set; }
    }

    public class StageFilterDTO
    {
        public int StageId { get; set; }
        public string StageDescription { get; set; }
        public int? LinkId { get; set; }
        public List<LayerFilterDTO>? Layers { get; set; }
        public List<SubStageFilterDTO>? SubStages { get; set; }
    }

    public class LayerFilterDTO
    {
        public int LayerId { get; set; }
        public string LayerDescription { get; set; }
        public int? LinkId { get; set; }

        public List<SubStageFilterDTO>? SubStages { get; set; }
    }

    public class SubStageFilterDTO
    {
        public int SubStageId { get; set; }
        public string SubStageDescription { get; set; }
        public int? LinkId { get; set; }
        public List<SubSpecialtyFilterDTO>? SubSpecialties { get; set; }
    }

    public class SubSpecialtyFilterDTO
    {
        public int SubSpecialtyId { get; set; }
        public string SubSpecialtyDescription { get; set; }
        public int LinkId { get; set; }
    }
}