namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>Catálogo de EO-RS / transportistas / escombreras / plantas de valorización.</summary>
public class SsResiduoEoRs
{
    public int Id { get; set; }
    public string Ruc { get; set; } = null!;
    public string RazonSocial { get; set; } = null!;
    /// <summary>TRANSPORTISTA | DISPOSICION_FINAL | VALORIZACION | COMERCIALIZADORA</summary>
    public string TipoOperador { get; set; } = null!;
    public string? NumeroRegistroMinam { get; set; }
    public DateOnly? VigenciaRegistro { get; set; }
    public string? Direccion { get; set; }
    /// <summary>MUNICIPAL | NO_MUNICIPAL</summary>
    public string? AmbitoGestion { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActualizadoEn { get; set; }

    public ICollection<SsResiduoEoRsDocumento> Documentos { get; set; } = [];
}
