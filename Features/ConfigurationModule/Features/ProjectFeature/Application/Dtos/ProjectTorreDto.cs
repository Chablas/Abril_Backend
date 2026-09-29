namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos
{
    public class ProjectTorreDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int CantidadSotanos { get; set; }
        public int CantidadPisos { get; set; }
        public int CantidadCisternas { get; set; }
    }

    public class ProjectTorreGuardarDto
    {
        public string Nombre { get; set; } = string.Empty;
        public int CantidadSotanos { get; set; }
        public int CantidadPisos { get; set; }
        public int CantidadCisternas { get; set; }
    }
}
