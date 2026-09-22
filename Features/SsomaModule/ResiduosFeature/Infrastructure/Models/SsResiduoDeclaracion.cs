using Abril_Backend.Features.CostsModule.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>Declaración anual (DAMRS/SIGERSOL) por razón social (contributor).</summary>
public class SsResiduoDeclaracion
{
    public int Id { get; set; }
    public int ContributorId { get; set; }
    public int PeriodoAnio { get; set; }
    /// <summary>BORRADOR | PRESENTADA</summary>
    public string Estado { get; set; } = "BORRADOR";
    public DateOnly? FechaPresentacion { get; set; }
    public string? ArchivoConstanciaUrl { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActualizadoEn { get; set; }

    public Contributor Contributor { get; set; } = null!;
    public ICollection<SsResiduoDeclaracionDetalle> Detalles { get; set; } = [];
    public ICollection<SsResiduoDeclaracionEoRs> EoRsIntervinientes { get; set; } = [];
}
