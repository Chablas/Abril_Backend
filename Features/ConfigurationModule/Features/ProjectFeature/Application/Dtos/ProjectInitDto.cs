using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    /// <summary>Una fila de los catálogos project_tipo y project_ciclo_vida.</summary>
    public class ProjectCatalogoDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
    }

    /// <summary>
    /// Carga inicial de Configuración → Proyectos: los catálogos de los filtros (y de los modales)
    /// y la primera página. Los cambios de filtro y de página piden solo la página (GET paged).
    /// </summary>
    public class ProjectInitDto
    {
        public List<ProjectCatalogoDto> Tipos { get; set; } = new();
        public List<ProjectCatalogoDto> CiclosVida { get; set; } = new();
        public PagedResult<ProjectDto> Proyectos { get; set; } = new();
    }
}
