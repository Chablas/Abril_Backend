using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.ArquitecturaComercialModule.Features.CostosFeature.Infrastructure.Models;

public static class PartidaCosto
{
    public const string ManoDeObra = "Mano de Obra";
    public const string Materiales = "Materiales";
    public const string Subcontrata = "Subcontrata";

    public static readonly string[] Valores = [ManoDeObra, Materiales, Subcontrata];

    public static bool EsValido(string partida) => Valores.Contains(partida);
}

/// <summary>Gasto real de una partida en una semana de un mes ya cerrado/en curso.</summary>
public class AcCostoRegistro
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public int Semana { get; set; }
    public string Partida { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Project? Proyecto { get; set; }
}

/// <summary>Proyección de gasto de una partida para el mes siguiente al que se está cerrando
/// (un solo monto total, no se desglosa por semana todavía).</summary>
public class AcCostoProyeccion
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Partida { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Project? Proyecto { get; set; }
}

/// <summary>Meta de presupuesto mensual, a nivel de toda la compañía (no por proyecto) —
/// referencia para el gráfico de evolución de gasto vs presupuesto meta.</summary>
public class AcCostoMetaMensual
{
    public int Id { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public decimal Monto { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
