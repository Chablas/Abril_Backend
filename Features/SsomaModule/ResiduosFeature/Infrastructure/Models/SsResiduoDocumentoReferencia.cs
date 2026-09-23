namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

/// <summary>
/// Repositorio de plantillas/manuales de referencia (manuales SIGERSOL, modelo de declaración
/// jurada, modelo de características de residuos sólidos), con versión y CRUD simple.
/// </summary>
public class SsResiduoDocumentoReferencia
{
    public int Id { get; set; }
    /// <summary>MANUAL_SIGERSOL | MODELO_DECLARACION_JURADA | MODELO_CARACTERISTICAS_RESIDUOS | OTRO</summary>
    public string Tipo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public string ArchivoUrl { get; set; } = null!;
    public string? Version { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
    public int CreadoPor { get; set; }
    public DateTimeOffset? ActualizadoEn { get; set; }
}
