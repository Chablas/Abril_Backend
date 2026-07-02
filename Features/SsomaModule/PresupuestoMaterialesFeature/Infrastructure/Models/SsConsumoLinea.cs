using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;

public class SsConsumoLinea
{
    public long Id { get; set; }
    public int CargaId { get; set; }
    public int ProjectId { get; set; }
    public string RecursoCrudo { get; set; } = null!;
    public int? ItemId { get; set; }
    public int? HitoId { get; set; }
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal PrecioTotal { get; set; }
    public DateOnly FechaGuia { get; set; }
    public bool Estandarizado { get; set; } = false;
    // SEED | MANUAL | FUZZY_CONFIRMADO
    public string? MetodoMatch { get; set; }
    public decimal? ScoreMatch { get; set; }
    public bool PerteneceSsoma { get; set; } = true;
    // null | PENDIENTE | AUTORIZADO | RECHAZADO
    public string? EstadoRevision { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;

    public SsConsumoCarga Carga { get; set; } = null!;
    public Project Proyecto { get; set; } = null!;
    public SsMaterialItem? Item { get; set; }
    public SsMaterialHito? Hito { get; set; }
}
