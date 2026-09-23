using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>Registro operativo por viaje/retiro (fuente única de verdad, GP-FOR-035).</summary>
public class SsResiduoViaje
{
    public long Id { get; set; }
    public int ProjectId { get; set; }
    public int? AutorizacionDmeId { get; set; }
    public int ResiduoTipoId { get; set; }
    public int EoRsId { get; set; }
    public string? Contratista { get; set; }
    /// <summary>DEMOLICION | EXCAVACION | CONSTRUCCION | OTRO</summary>
    public string? Origen { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal CantidadM3 { get; set; }
    /// <summary>Calculado desde SsResiduoTipoFactor, editable ante ajuste manual.</summary>
    public decimal? CantidadTon { get; set; }
    /// <summary>ALMACENADO | TRATADO | ACONDICIONADO | VALORIZADO | COMERCIALIZADO | DISPOSICION_FINAL</summary>
    public string TipoManejo { get; set; } = null!;
    public string? GestorReceptor { get; set; }
    public string? DestinoFinal { get; set; }
    public string? NumeroRegistro { get; set; }
    public string? NumeroCertificado { get; set; }
    public string? ArchivoUrl { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public int CreadoPor { get; set; }
    public DateTimeOffset? ActualizadoEn { get; set; }

    public Project Proyecto { get; set; } = null!;
    public SsResiduoAutorizacionDme? AutorizacionDme { get; set; }
    public SsResiduoTipo ResiduoTipo { get; set; } = null!;
    public SsResiduoEoRs EoRs { get; set; } = null!;
}
