using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>Constancias de disposición final / eliminación, periodicidad mensual, por contratista y destino.</summary>
public class SsResiduoConstancia
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? EoRsId { get; set; }
    public string? Contratista { get; set; }
    public string? Destino { get; set; }
    public int PeriodoAnio { get; set; }
    public int PeriodoMes { get; set; }
    public string? NumeroCertificado { get; set; }
    public string ArchivoUrl { get; set; } = null!;
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public int CreadoPor { get; set; }

    public Project Proyecto { get; set; } = null!;
    public SsResiduoEoRs? EoRs { get; set; }
}
