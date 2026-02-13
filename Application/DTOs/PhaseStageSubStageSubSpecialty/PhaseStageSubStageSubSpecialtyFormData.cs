namespace Abril_Backend.Application.DTOs
{
    public class PhaseStageSubStageSubSpecialtyFormDataDTO
    {
        public List<AreaDTO> Areas { get; set; }
        public List<ProjectDTO> Projects { get; set; }
        public List<PhaseDTO> Phases { get; set; }
        public List<StageDTO> Stages { get; set; }
        public List<SubStageDTO> SubStages { get; set; }
        public List<SubSpecialtyDTO> SubSpecialties { get; set; }
    }
}