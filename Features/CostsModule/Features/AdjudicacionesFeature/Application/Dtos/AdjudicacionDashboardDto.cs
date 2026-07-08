namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    /// <summary>Ítem genérico para gráficos (id opcional + etiqueta + valor).</summary>
    public class AdjudicacionChartItemDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = null!;
        public decimal Value { get; set; }
    }

    /// <summary>
    /// Ítem del gráfico "por estado" con el detalle breve de las adjudicaciones en ese estado
    /// ("CONTRATISTA — PARTIDA"), para mostrarlo en el tooltip de la barra.
    /// </summary>
    public class AdjudicacionEstadoChartItemDto : AdjudicacionChartItemDto
    {
        public List<string> Items { get; set; } = new();
    }

    /// <summary>Monto adjudicado acumulado por moneda.</summary>
    public class AdjudicacionMoneyByCurrencyDto
    {
        public string Code { get; set; } = null!;
        public string Symbol { get; set; } = null!;
        public decimal Total { get; set; }
    }

    /// <summary>Tarjetas resumen del dashboard.</summary>
    public class AdjudicacionDashboardSummaryDto
    {
        public int Total { get; set; }
        public int Completadas { get; set; }
        public int EnProceso { get; set; }
        public int TotalProyectos { get; set; }
        public decimal MontoPenTotal { get; set; }
        public decimal MontoUsdTotal { get; set; }
        public int PlazoPromedioDias { get; set; }
    }

    /// <summary>Opción genérica para los desplegables de filtros del dashboard (id + etiqueta).</summary>
    public class AdjudicacionOptionDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = null!;
    }

    /// <summary>
    /// Catálogos para los filtros del dashboard. Solo se envían en la primera carga
    /// (no se re-piden cuando el usuario cambia un filtro).
    /// </summary>
    public class AdjudicacionDashboardFiltersDto
    {
        public List<AdjudicacionOptionDto> Projects { get; set; } = new();
        public List<AdjudicacionOptionDto> ContractTypes { get; set; } = new();
        public List<AdjudicacionOptionDto> ContractModalities { get; set; } = new();
        public List<AdjudicacionOptionDto> PaymentMethods { get; set; } = new();
        public List<AdjudicacionOptionDto> Statuses { get; set; } = new();
    }

    /// <summary>Datos completos del dashboard de adjudicaciones (un solo endpoint).</summary>
    public class AdjudicacionDashboardDto
    {
        /// <summary>Catálogos de filtros (solo en la primera carga; null cuando el cliente ya los tiene).</summary>
        public AdjudicacionDashboardFiltersDto? Filters { get; set; }
        public AdjudicacionDashboardSummaryDto Summary { get; set; } = new();
        public List<AdjudicacionEstadoChartItemDto> PorEstado { get; set; } = new();
        public List<AdjudicacionChartItemDto> PorProyecto { get; set; } = new();
        public List<AdjudicacionChartItemDto> PorTipoContrato { get; set; } = new();
        public List<AdjudicacionChartItemDto> PorCategoria { get; set; } = new();
        public List<AdjudicacionChartItemDto> PorModalidad { get; set; } = new();
        /// <summary>Cantidad de adjudicaciones por modalidad de pago (Adenda, Contrato con/sin adelanto).</summary>
        public List<AdjudicacionChartItemDto> PorModalidadPago { get; set; } = new();
        /// <summary>Paso 5: contratos llegados a Of. Central con vs sin observaciones.</summary>
        public List<AdjudicacionChartItemDto> LlegadaObservaciones { get; set; } = new();
        public List<AdjudicacionChartItemDto> PorMes { get; set; } = new();
        public List<AdjudicacionMoneyByCurrencyDto> MontoPorMoneda { get; set; } = new();
        /// <summary>Top subcontratistas por monto adjudicado en soles (PEN).</summary>
        public List<AdjudicacionChartItemDto> TopSubcontratistasPen { get; set; } = new();
        /// <summary>Top subcontratistas por monto adjudicado en dólares (USD).</summary>
        public List<AdjudicacionChartItemDto> TopSubcontratistasUsd { get; set; } = new();
        /// <summary>Top subcontratistas por cantidad de adjudicaciones.</summary>
        public List<AdjudicacionChartItemDto> TopContratistas { get; set; } = new();
    }
}
