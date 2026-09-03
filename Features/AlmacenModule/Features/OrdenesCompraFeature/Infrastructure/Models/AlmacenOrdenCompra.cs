using Abril_Backend.Shared.Models;

namespace Abril_Backend.Features.AlmacenModule.Features.OrdenesCompraFeature.Infrastructure.Models;

public static class TipoDocumentoOrdenCompra
{
    public const string OrdenCompra = "Orden de Compra";
    public const string Contrato = "Contrato";

    public static readonly string[] Valores = [OrdenCompra, Contrato];

    public static bool EsValido(string tipo) => Valores.Contains(tipo);
}

/// <summary>Orden de compra o contrato de un proveedor/subcontratista para un proyecto,
/// con el archivo adjunto (PDF/Excel/Word) y quién lo subió — control de almacén nuevo e
/// independiente del módulo Costos (Adjudicaciones). Cuando el proveedor SÍ es un
/// subcontratista ya registrado en Costos, se guarda solo su Id como referencia de
/// solo lectura (<see cref="ContratistaId"/>) — no hay FK ni se toca esa tabla.</summary>
public class AlmacenOrdenCompra
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Proveedor { get; set; } = string.Empty;

    /// <summary>Referencia opcional, de solo lectura, al Contractor de Costos (Adjudicaciones)
    /// cuando el proveedor ya existe en ese catálogo. Sin FK: evita cualquier acoplamiento
    /// de esquema con esa tabla.</summary>
    public int? ContratistaId { get; set; }

    public decimal Monto { get; set; }
    public string Moneda { get; set; } = "PEN";
    public DateTime Fecha { get; set; }
    public string ArchivoUrl { get; set; } = string.Empty;
    public string ArchivoNombre { get; set; } = string.Empty;
    public string? SubidoPor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project? Proyecto { get; set; }
}
