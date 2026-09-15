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

/// <summary>Presupuesto aprobado por partida para todo el proyecto (no por mes) — el techo
/// contra el que se mide el gasto real acumulado (suma de AcCostoRegistro de todos los
/// meses) para sacar la desviación %. Reemplaza el control manual que se hacía en Excel.</summary>
public class AcCostoPresupuesto
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string Partida { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Project? Proyecto { get; set; }
}

/// <summary>Marca un mes de un proyecto como cerrado — a partir de acá ni el registro semanal
/// ni la proyección de ese periodo se pueden editar (control real, no un Excel donde cualquiera
/// corrige un mes pasado sin dejar rastro). Reabrir el periodo borra esta fila.</summary>
public class AcCostoCierre
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string? CerradoPor { get; set; }
    public DateTime CerradoEn { get; set; } = DateTime.UtcNow;

    public Project? Proyecto { get; set; }
}
