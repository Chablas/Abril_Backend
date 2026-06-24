namespace Abril_Backend.Features.Costs.Adjudicaciones.Application.Dtos
{
    /// <summary>Ítem genérico para gráficos (id opcional + etiqueta + valor).</summary>
    public class AdjudicacionChartItemDto
    {
        public int Id { get; set; }
        public string Label { get; set; } = null!;
        public decimal Value { get; set; }
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

    /// <summary>Datos completos del dashboard de adjudicaciones (un solo endpoint).</summary>
    public class AdjudicacionDashboardDto
    {
        public AdjudicacionDashboardSummaryDto Summary { get; set; } = new();
        public List<AdjudicacionChartItemDto> PorEstado { get; set; } = new();
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
