using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>Constancia final de obra (cierre de proyecto o de periodo declarado).</summary>
public class SsResiduoConstanciaFinal
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public DateOnly FechaEmision { get; set; }
    public string ArchivoUrl { get; set; } = null!;
    public string? Observaciones { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public int CreadoPor { get; set; }

    public Project Proyecto { get; set; } = null!;
}
