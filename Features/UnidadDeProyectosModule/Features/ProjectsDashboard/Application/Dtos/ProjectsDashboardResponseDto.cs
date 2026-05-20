namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Dtos
{
    public class ProjectsDashboardResponseDto
    {
        public int TotalProyectos { get; set; }
        public int AlDia { get; set; }
        public int ConRetraso { get; set; }
        public int SinActividades { get; set; }
        public double PorcentajeAvancePromedio { get; set; }
        public List<ProyectoDetalleDto> Proyectos { get; set; } = new();
    }

    public class ProyectoDetalleDto
    {
        public int ProjectId { get; set; }
        public string? ProjectDescription { get; set; }
        public string? Estado { get; set; }
        public string? ResponsableArqCom { get; set; }
        public int TotalActividades { get; set; }
        public int Culminadas { get; set; }
        public int EnProceso { get; set; }
        public int Vencidas { get; set; }
        public double PorcentajeAvance { get; set; }
        public bool EstaConRetraso { get; set; }
    }
}
