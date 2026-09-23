namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>Catálogo de tipos de residuo (codificados según SIGERSOL/MINAM).</summary>
public class SsResiduoTipo
{
    public int Id { get; set; }
    public string? CodigoSigersol { get; set; }
    public string Nombre { get; set; } = null!;
    public bool EsPeligroso { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SsResiduoTipoFactor> Factores { get; set; } = [];
}
