namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;

public class SsMaterialAlias
{
    public int Id { get; set; }
    public string TextoCrudo { get; set; } = null!;
    public string TextoCrudoNorm { get; set; } = null!;
    public int ItemId { get; set; }
    // SEED | MANUAL | FUZZY_CONFIRMADO
    public string Origen { get; set; } = null!;
    public decimal? Confianza { get; set; }
    public int? ConfirmadoPor { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public SsMaterialItem Item { get; set; } = null!;
}
