namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>Factor de conversión m3 -> t de un tipo de residuo, vigente por rango de fechas.</summary>
public class SsResiduoTipoFactor
{
    public int Id { get; set; }
    public int ResiduoTipoId { get; set; }
    public decimal FactorM3aTon { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public SsResiduoTipo ResiduoTipo { get; set; } = null!;
}
