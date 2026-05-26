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
        public List<EstadoDistribucionDto> DistribucionPorEstado { get; set; } = new();
        public List<ResponsableRankingDto> RankingResponsables { get; set; } = new();
        public List<HeatmapCargaItemDto> HeatmapCarga { get; set; } = new();
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
        public int DiasRetraso { get; set; }
        public string Semaforo { get; set; } = "verde";
        public string? EtapaNombre { get; set; }
    }

    public class EstadoDistribucionDto
    {
        public string Estado { get; set; } = string.Empty;
        public int CantidadProyectos { get; set; }
    }

    public class ResponsableRankingDto
    {
        public int ResponsableId { get; set; }
        public string? ResponsableNombre { get; set; }
        public int TotalProyectos { get; set; }
        public int ActividadesCompletadas { get; set; }
        public int ActividadesVencidas { get; set; }
        public int TotalActividades { get; set; }
        public double Score { get; set; }
    }

    public class HeatmapCargaItemDto
    {
        public int ResponsableId { get; set; }
        public string? ResponsableNombre { get; set; }
        public string Semana { get; set; } = string.Empty;
        public int CantidadActividades { get; set; }
    }

    public class ProyectoDetailDashboardDto
    {
        public ProyectoDetailKpisDto Kpis { get; set; } = new();
        public List<ActividadVencidaDto> ActividadesVencidas { get; set; } = new();
        public List<ActividadGanttDto> Gantt { get; set; } = new();
    }

    public class ProyectoDetailKpisDto
    {
        public int TotalActividades { get; set; }
        public int Culminadas { get; set; }
        public int EnProceso { get; set; }
        public int Vencidas { get; set; }
        public double AvancePct { get; set; }
        public int DiasRetraso { get; set; }
        public string Semaforo { get; set; } = "verde";
    }

    public class ActividadVencidaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Tipo { get; set; }
        public string? ResponsableNombre { get; set; }
        public DateOnly? FinProgramado { get; set; }
        public int DiasRetraso { get; set; }
    }

    public class ActividadGanttDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateOnly? InicioProgramado { get; set; }
        public DateOnly? FinProgramado { get; set; }
        public DateOnly? FinEfectivo { get; set; }
        public string? Estado { get; set; }
        public string? ResponsableNombre { get; set; }
    }
}
