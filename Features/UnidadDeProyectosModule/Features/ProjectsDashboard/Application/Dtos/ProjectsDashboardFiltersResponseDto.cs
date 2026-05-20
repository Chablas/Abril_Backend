using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Dtos
{
    public class ProjectsDashboardFiltersResponseDto
    {
        public List<ProjectSimpleDTO> Projects { get; set; } = new();
        public List<string> Estados { get; set; } = new();
        public List<ResponsableArqComSimpleDto> ResponsablesArqCom { get; set; } = new();
    }

    public class ResponsableArqComSimpleDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
    }
}
