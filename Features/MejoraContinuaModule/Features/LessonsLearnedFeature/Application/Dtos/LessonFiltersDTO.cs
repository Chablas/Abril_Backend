using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Dtos
{
    /// <summary>
    /// DTO legacy de filtros usado por endpoints antiguos. Las referencias a
    /// AreaDTO/ProjectDTO/etc. siguen viviendo en Abril_Backend.Application.DTOs
    /// porque son DTOs compartidos entre features.
    /// </summary>
    public class LessonFiltersDTO
    {
        public List<ProjectDTO> Projects { get; set; } = new();
        public List<AreaDTO> Areas { get; set; } = new();
        public List<AreaDTO> Phases { get; set; } = new();
        public List<StageDTO> Stages { get; set; } = new();
        public List<LayerDTO> Layers { get; set; } = new();
        public List<SubStageDTO> SubStages { get; set; } = new();
        public List<SubSpecialtyDTO> SubSpecialties { get; set; } = new();
    }
}
