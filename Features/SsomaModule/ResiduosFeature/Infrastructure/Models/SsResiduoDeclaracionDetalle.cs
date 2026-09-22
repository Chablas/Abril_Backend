namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>
/// Detalle por tipo de residuo: cantidad acumulada del periodo anterior + generación mensual
/// (ene-dic), calculado desde ss_residuo_viaje y editable antes de marcar la declaración como
/// presentada.
/// </summary>
public class SsResiduoDeclaracionDetalle
{
    public int Id { get; set; }
    public int DeclaracionId { get; set; }
    public int ResiduoTipoId { get; set; }
    public decimal CantidadAcumuladaAnterior { get; set; }
    public decimal Ene { get; set; }
    public decimal Feb { get; set; }
    public decimal Mar { get; set; }
    public decimal Abr { get; set; }
    public decimal May { get; set; }
    public decimal Jun { get; set; }
    public decimal Jul { get; set; }
    public decimal Ago { get; set; }
    public decimal Sep { get; set; }
    public decimal Oct { get; set; }
    public decimal Nov { get; set; }
    public decimal Dic { get; set; }

    // Desglose por tipo de manejo (debe cuadrar contra el total generado).
    public decimal Almacenado { get; set; }
    public decimal Tratado { get; set; }
    public decimal Acondicionado { get; set; }
    public decimal Valorizado { get; set; }
    public decimal Comercializado { get; set; }
    public decimal DisposicionFinal { get; set; }

    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActualizadoEn { get; set; }

    public SsResiduoDeclaracion Declaracion { get; set; } = null!;
    public SsResiduoTipo ResiduoTipo { get; set; } = null!;
}
