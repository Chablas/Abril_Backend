namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.ObservacionesFeature.Infrastructure.Models;

/// <summary>
/// Ítem de catálogo reusado por los combos de Observaciones (Partida, Área
/// Responsable). Un solo tipo de tabla con discriminador <see cref="Tipo"/> en
/// vez de una tabla por catálogo, porque ambos son listas planas idénticas en
/// forma (nombre + orden + activo) — solo cambia qué lista de valores contienen.
/// </summary>
public class AcCatalogoItem
{
    public int Id { get; set; }

    /// <summary>"Partida" o "AreaResponsable". Ver <see cref="AcCatalogoTipo"/>.</summary>
    public string Tipo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class AcCatalogoTipo
{
    public const string Partida = "Partida";
    public const string AreaResponsable = "AreaResponsable";

    public static bool EsValido(string tipo) => tipo == Partida || tipo == AreaResponsable;
}
