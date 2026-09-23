namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>
/// EO-RS intervinientes declarados por cada declaración (recolección/transporte, tratamiento,
/// valorización), con su N° de servicios y total transportado al año.
/// </summary>
public class SsResiduoDeclaracionEoRs
{
    public int Id { get; set; }
    public int DeclaracionId { get; set; }
    public int EoRsId { get; set; }
    /// <summary>RECOLECCION_TRANSPORTE | TRATAMIENTO | VALORIZACION</summary>
    public string Etapa { get; set; } = null!;
    public int NumeroServiciosAnio { get; set; }
    public decimal TotalResiduoTon { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public SsResiduoDeclaracion Declaracion { get; set; } = null!;
    public SsResiduoEoRs EoRs { get; set; } = null!;
}
