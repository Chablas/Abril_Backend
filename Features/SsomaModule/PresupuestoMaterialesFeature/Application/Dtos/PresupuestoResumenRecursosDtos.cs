namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

/// <summary>Fila del "Desagregado de Recursos" que exporta Costos — une las 5 fuentes de costo del
/// presupuesto (Materiales por ratio, Personal por hito, Vigilancia por hito, Servicios fijos, Kits)
/// en un mismo formato de fila: Cantidad × PU (=Mo+Mat+Equ+Sc+Her) siempre reconcilia con
/// CostoDirecto, aunque el cálculo real de origen tenga más factores (ej. Personal es
/// cantidad×tarifa×semanas — acá Cantidad ya viene combinada como personas×semanas para que la
/// columna PU sea una tarifa semanal real y el producto cierre exacto).</summary>
public class RecursoResumenLineaDto
{
    /// <summary>De dónde sale la fila (MATERIALES/PERSONAL/VIGILANCIA/SERVICIOS FIJOS/KITS) — se
    /// muestra como columna informativa, no como fila de encabezado: el listado va todo junto y
    /// numerado en secuencia, igual que el Desagregado de Recursos real (una fila por partida
    /// concreta, sin agrupar).</summary>
    public string Grupo { get; set; } = "";
    public string Item { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string? Unidad { get; set; }
    public decimal Cantidad { get; set; }
    public decimal Mo { get; set; }
    public decimal Mat { get; set; }
    public decimal Equ { get; set; }
    public decimal Sc { get; set; }
    public decimal Her { get; set; }
    public decimal Pu => Mo + Mat + Equ + Sc + Her;
    public decimal CostoDirecto { get; set; }
}

public class PresupuestoResumenRecursosDto
{
    public int PresupuestoId { get; set; }
    public int ProjectId { get; set; }
    public string ProjectDescription { get; set; } = "";
    public int Version { get; set; }
    public string Estado { get; set; } = "";
    public List<RecursoResumenLineaDto> Lineas { get; set; } = [];

    // Totales extendidos por categoría (Cantidad × costo unitario de esa categoría) — igual que las
    // columnas MO/MAT/EQU de la derecha en el Desagregado de Recursos de referencia.
    public decimal TotalMo  => Lineas.Sum(l => l.Mo  * l.Cantidad);
    public decimal TotalMat => Lineas.Sum(l => l.Mat * l.Cantidad);
    public decimal TotalEqu => Lineas.Sum(l => l.Equ * l.Cantidad);
    public decimal TotalSc  => Lineas.Sum(l => l.Sc  * l.Cantidad);
    public decimal TotalHer => Lineas.Sum(l => l.Her * l.Cantidad);
    public decimal TotalCostoDirecto => Lineas.Sum(l => l.CostoDirecto);
}
