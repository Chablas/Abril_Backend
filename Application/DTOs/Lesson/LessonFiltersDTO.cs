

namespace Abril_Backend.Application.DTOs
{
    public class LessonFiltersDTO  {
        public List<ProjectDTO> Projects {get; set;}
        public List<AreaDTO> Areas {get; set;}
        public List<AreaDTO> Phases {get; set;}
        public List<StageDTO> Stages {get; set;}
        public List<LayerDTO> Layers {get; set;}
        public List<SubStageDTO> SubStages {get; set;}
        public List<SubSpecialtyDTO> SubSpecialties {get; set;}
    }
}