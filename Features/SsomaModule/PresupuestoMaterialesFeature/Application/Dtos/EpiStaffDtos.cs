namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

/// <summary>Config global (una sola fila, para toda la empresa) de cuánto EPI compartido con
/// obrero rota por miembro de Staff. rotacion_*_meses = cada cuántos meses se repone 1 unidad
/// por persona (lentes=1 → 1/mes; barbiquejo=3 → 1 cada 3 meses). Arnés no tiene componente de
/// tiempo: se entrega una sola vez por staff, igual que casco/orejera.</summary>
public class EpiStaffConfigDto
{
    public decimal ArnesPorStaff { get; set; }
    public decimal RotacionLentesMeses { get; set; }
    public decimal RotacionBarbiquejoMeses { get; set; }
    public decimal RotacionGuantesMeses { get; set; }
}

public class ActualizarEpiStaffConfigDto
{
    public decimal ArnesPorStaff { get; set; }
    public decimal RotacionLentesMeses { get; set; }
    public decimal RotacionBarbiquejoMeses { get; set; }
    public decimal RotacionGuantesMeses { get; set; }
}

/// <summary>Una fila de EPI de Staff — para las que comparten SKU con obrero (Casco, Orejera,
/// Arnés, Lentes, Barbiquejo, Guantes) RequiereDescuento=true y CantidadObrero = CantidadTotal −
/// CantidadStaff (piso en 0), para no duplicar el costo entre las dos líneas del Desagregado de
/// Recursos. Para las exclusivas de staff (Camisa/Blusa/Zapato) RequiereDescuento=false: el 100%
/// de lo consumido ya es de Staff, no hay nada que restarle a Obrero.</summary>
public class EpiStaffLineaDto
{
    public string Nombre { get; set; } = "";
    public int? FamiliaId { get; set; }
    public bool RequiereDescuento { get; set; }
    public decimal CantidadTotalConsumida { get; set; }
    public decimal CantidadStaff { get; set; }
    public decimal CantidadObrero { get; set; }
    /// <summary>Precio de la variante de Staff — para Casco/Orejera es el precio real de las
    /// líneas BLANCO/INGENIER/3M (no el promedio ciego de la família, que mezcla también los
    /// colores/marcas de obrero y sale mucho más bajo). Para Arnés/Lentes/Barbiquejo/Guantes,
    /// donde Staff y Obrero comparten el mismo SKU, es igual a PrecioUnitarioObrero.</summary>
    public decimal PrecioUnitarioStaff { get; set; }
    /// <summary>Precio de la variante de Obrero — para Casco/Orejera es el promedio de las líneas
    /// que NO son BLANCO/INGENIER/3M (los colores/marcas de obrero, sin mezclar el precio premium
    /// de Staff hacia abajo ni hacia arriba).</summary>
    public decimal PrecioUnitarioObrero { get; set; }
    public decimal CostoStaff { get; set; }
    public decimal CostoObrero { get; set; }
}

public class EpiStaffCalculoDto
{
    public int ProjectId { get; set; }
    /// <summary>Mediana histórica de consumo de casco blanco/ingeniero (driver STAFF_CASCO) ×
    /// Área techada del proyecto — mismo mecanismo que ya usan HH/Trabajadores.</summary>
    public decimal StaffHeadcountAplicado { get; set; }
    /// <summary>(último hito del cronograma vigente − primer hito) / 30.44 — misma fuente que
    /// usan Personal/Vigilancia para "Semanas".</summary>
    public decimal MesesProyecto { get; set; }
    public EpiStaffConfigDto Config { get; set; } = new();
    public List<EpiStaffLineaDto> Lineas { get; set; } = [];
}
