namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Models;

/// <summary>Servicio de vigilancia externa planificado por hito crítico del cronograma real del
/// proyecto — se factura por punto/turno cubierto (no por vigilante individual como el rol interno
/// VIGIA de Dotación de personal). El precio unitario se toma del ratio ya calculado en Ratios para
/// la família "Servicio de Vigilancia" (Catálogo), snapshot al momento de guardar.</summary>
public class SsPresupuestoVigilanciaHito
{
    public int Id { get; set; }
    public int PresupuestoId { get; set; }
    /// <summary>Apunta al hito REAL del cronograma del proyecto (MilestoneSchedule) — etapa de ingreso.</summary>
    public int HitoId { get; set; }
    /// <summary>Etapa de salida (opcional) — si está seteada, Semanas se calcula desde las fechas reales.</summary>
    public int? HitoSalidaId { get; set; }
    public int CantidadPuntos { get; set; }
    public decimal Semanas { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }
}
