using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos
{
    public class LessonFiltersFormDataDTO
    {
        public List<AreaSimpleDTO> Areas { get; set; }
        public List<ProjectSimpleDTO> Projects { get; set; }
        public List<LessonPeriodDTO> Periods { get; set; }
        public List<PhaseSimpleDTO> Phases { get; set; }
        public List<StageSimpleDTO> Stages { get; set; }
        public List<LayerSimpleDTO> Layers { get; set; }
        public List<SubStageSimpleDTO> SubStages { get; set; }
        public List<SubSpecialtySimpleDTO> SubSpecialties { get; set; }
        public List<UserFilterDTO> Users { get; set; }
    }
}
