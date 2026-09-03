using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Models;

public static class TipoMovimientoAlmacen
{
    public const string Ingreso = "Ingreso";
    public const string Salida = "Salida";

    public static readonly string[] Valores = [Ingreso, Salida];

    public static bool EsValido(string tipo) => Valores.Contains(tipo);
}

/// <summary>Catálogo de materiales/insumos de almacén — independiente de cualquier
/// catálogo de partidas de otros módulos (Costos, SSOMA, etc.).</summary>
public class AlmacenMaterial
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    /// <summary>Umbral para disparar una nueva orden de compra. Null = material sin
    /// seguimiento de stock crítico todavía (no aparece en el dashboard de críticos).</summary>
    public decimal? PuntoReorden { get; set; }

    /// <summary>Umbral mínimo absoluto — por debajo de esto el estado es "Crítico".</summary>
    public decimal? StockSeguridad { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Ingreso o salida de un material de almacén hacia/desde un proyecto — reemplaza
/// el registro manual en Excel. El saldo por proyecto+material se calcula a partir de estos
/// movimientos, no se guarda como campo (evita inconsistencias).</summary>
public class AlmacenMovimiento
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public int MaterialId { get; set; }
    public DateTime Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }

    /// <summary>Origen del ingreso (proveedor) o destino de la salida cuando aplica —
    /// texto libre, no un catálogo formal en esta primera versión.</summary>
    public string? Origen { get; set; }

    public string? Comentario { get; set; }
    public string? CreadoPor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project? Proyecto { get; set; }
    public AlmacenMaterial? Material { get; set; }
}
