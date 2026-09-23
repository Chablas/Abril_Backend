using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>Autorización de Disposición de Material Excedente (DME) por obra.</summary>
public class SsResiduoAutorizacionDme
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Municipalidad { get; set; } = null!;
    public string NumeroResolucion { get; set; } = null!;
    public int? EscombreraDestinoId { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly VigenciaHasta { get; set; }
    public string? PlacasAutorizadas { get; set; }
    /// <summary>VIGENTE | VENCIDA | ANULADA</summary>
    public string Estado { get; set; } = "VIGENTE";
    public string? ArchivoUrl { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActualizadoEn { get; set; }

    public Project Proyecto { get; set; } = null!;
    public SsResiduoEoRs? EscombreraDestino { get; set; }
}
