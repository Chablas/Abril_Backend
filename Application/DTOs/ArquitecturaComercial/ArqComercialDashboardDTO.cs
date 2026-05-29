namespace Abril_Backend.Application.DTOs.ArquitecturaComercial
{
    public class ArqComercialKpiDTO
    {
        public int TotalActividades { get; set; }
        public int Culminadas { get; set; }
        public int EnProceso { get; set; }
        public int Vencidas { get; set; }
        public int Pendientes { get; set; }
        public double EficienciaMedia { get; set; }
        public double ProgresoGlobal { get; set; }
    }

    public class ArqComercialAlertDTO
    {
        public int VencidasSinCerrar { get; set; }
        public int VencenEstaSemana { get; set; }
        public int ArrancanEstaSemana { get; set; }
        public int HitosProximos14Dias { get; set; }
    }

    public class ArqComercialChartItemDTO
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
    }

    public class ProyeccionAvanceDTO
    {
        public List<string> Labels { get; set; } = new();
        public List<double> Programado { get; set; } = new();
        public List<double> Real { get; set; } = new();
        public List<double> Proyeccion { get; set; } = new();
    }

    public class EficienciaSemanalDTO
    {
        public string Semana { get; set; } = string.Empty;
        public double Valor { get; set; }
    }

    public class SupervisorProgresoDTO
    {
        public string Nombre { get; set; } = string.Empty;
        public double Progreso { get; set; }
        public int Total { get; set; }
        public int Completadas { get; set; }
    }

    public class HitoCriticoDTO
    {
        public int    Id            { get; set; }
        public string Nombre        { get; set; } = string.Empty;
        public string Proyecto      { get; set; } = string.Empty;
        public string FechaLimite   { get; set; } = string.Empty;
        public int    DiasRestantes { get; set; }
        public string Estado        { get; set; } = string.Empty;
    }

    public class ArqComercialDashboardDTO
    {
        public ArqComercialKpiDTO Kpis { get; set; } = new();
        public ArqComercialAlertDTO Alertas { get; set; } = new();
        public ProyeccionAvanceDTO ProyeccionAvance { get; set; } = new();
        public List<ArqComercialChartItemDTO> RankingEficiencia { get; set; } = new();
        public List<ArqComercialChartItemDTO> DistribucionEstado { get; set; } = new();
        public List<EficienciaSemanalDTO> TendenciaEficiencia { get; set; } = new();
        public List<SupervisorProgresoDTO> Supervisores { get; set; } = new();
        public List<HitoCriticoDTO> HitosCriticos { get; set; } = new();
        public TareasPorArquitectoDTO[] TareasPorArquitectoDetalle { get; set; } = [];
        public AvanceSemanalDTO[]       AvanceSemanal              { get; set; } = [];
        public EficienciaSpiDTO[]       EficienciaSpi              { get; set; } = [];
        public CategoriaItemDTO[]       Categorias                 { get; set; } = [];
    }

    public class ArqComercialFilterOptionDTO
    {
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class ArqComercialProjectOptionDTO
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }

    public class ArqComercialFiltersDTO
    {
        public List<ArqComercialFilterOptionDTO> Semanas { get; set; } = new();
        public List<ArqComercialFilterOptionDTO> Meses { get; set; } = new();
        public List<ArqComercialProjectOptionDTO> Proyectos { get; set; } = new();
    }
}
