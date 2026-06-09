namespace Abril_Backend.Features.Habilitacion.Application.Dtos.Dashboard
{
    public class DashboardAdminDto
    {
        public DashboardKpisDto Kpis { get; set; } = new();
        public List<EmpresaRiesgoDto> EmpresasEnRiesgo { get; set; } = new();
        public List<WorkerRiesgoDto> WorkersEnRiesgo { get; set; } = new();
        public List<ProyectoEstadoDto> EstadoPorProyecto { get; set; } = new();
        public List<VencimientoProximoDto> VencimientosProximos { get; set; } = new();
    }

    public class DashboardKpisDto
    {
        public int EmpresasHabilitadas { get; set; }
        public int EmpresasTotal { get; set; }
        public int WorkersHabilitados { get; set; }
        public int WorkersTotal { get; set; }
        public int EntregablesVencidos { get; set; }
        public int EntregablesPorVencer30 { get; set; }
        public int EmosPorVencer30 { get; set; }
        public int SctrPorVencer15 { get; set; }
    }

    public class EmpresaRiesgoDto
    {
        public int EmpresaId { get; set; }
        public string Nombre { get; set; } = "";
        public int EntregablesVencidos { get; set; }
        public int EntregablesPorVencer { get; set; }
        public int WorkersActivos { get; set; }
        public string NivelRiesgo { get; set; } = "";
    }

    public class WorkerRiesgoDto
    {
        public int WorkerId { get; set; }
        public string Nombre { get; set; } = "";
        public string Empresa { get; set; } = "";
        public string Proyecto { get; set; } = "";
        public List<string> DocumentosVencidos { get; set; } = new();
        public List<string> DocumentosPorVencer { get; set; } = new();
        public bool SinEmo { get; set; }
        public bool SinInduccion { get; set; }
    }

    public class ProyectoEstadoDto
    {
        public int ProyectoId { get; set; }
        public string Nombre { get; set; } = "";
        public int WorkersTotal { get; set; }
        public int WorkersHabilitados { get; set; }
        public int EmpresasActivas { get; set; }
        public int EntregablesPendientes { get; set; }
    }

    public class VencimientoProximoDto
    {
        public string Tipo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string Entidad { get; set; } = "";
        public string Proyecto { get; set; } = "";
        public DateTime FechaVencimiento { get; set; }
        public int DiasRestantes { get; set; }
    }
}
