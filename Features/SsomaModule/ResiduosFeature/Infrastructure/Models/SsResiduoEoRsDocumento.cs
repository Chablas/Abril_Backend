namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>Checklist documentario por EO-RS (SOAT, tarjeta de circulación, licencia, etc.) con vigencia.</summary>
public class SsResiduoEoRsDocumento
{
    public int Id { get; set; }
    public int EoRsId { get; set; }
    /// <summary>SOAT | TARJETA_CIRCULACION | LICENCIA_CONDUCIR | REGISTRO_EO_RS | PERMISO_MTC_PELIGROSOS | OTRO</summary>
    public string TipoDocumento { get; set; } = null!;
    public string? Numero { get; set; }
    public DateOnly? VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
    public string? ArchivoUrl { get; set; }
    public bool Cumple { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActualizadoEn { get; set; }

    public SsResiduoEoRs EoRs { get; set; } = null!;
}
