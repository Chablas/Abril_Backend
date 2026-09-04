namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

public class KitResumenDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int TipoId { get; set; }
    public string NombreTipo { get; set; } = "";
}

public class KitItemDto
{
    public int Id { get; set; }
    public int FamiliaId { get; set; }
    public string NombreFamilia { get; set; } = "";
    public decimal CantidadPorKit { get; set; }
    public bool EsConsumible { get; set; }
}

public class KitDetalleDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public int TipoId { get; set; }
    public string NombreTipo { get; set; } = "";
    public List<KitItemDto> Items { get; set; } = [];
}

public class KitItemInputDto
{
    public int FamiliaId { get; set; }
    public decimal CantidadPorKit { get; set; }
    public bool EsConsumible { get; set; } = true;
}

public class KitCreateDto
{
    public string Nombre { get; set; } = "";
    public int TipoId { get; set; }
    public List<KitItemInputDto> Items { get; set; } = [];
}

/// <summary>Resultado de multiplicar el BOM del kit por la cantidad de kits que necesita el proyecto.
/// PrecioUnitario/Total ya vienen poblados tanto en la vista previa (CalcularAsync, precio en vivo
/// desde Ratios) como en lo YA GUARDADO en el presupuesto (snapshot al momento de guardar) — pueden
/// diferir levemente si el precio de Ratios cambió entre el cálculo y el guardado.</summary>
public class KitCalculoLineaDto
{
    public int FamiliaId { get; set; }
    public string NombreFamilia { get; set; } = "";
    public decimal CantidadPorKit { get; set; }
    public decimal CantidadTotal { get; set; }
    public bool EsConsumible { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }
}

public class KitProyectoGuardarDto
{
    public int KitId { get; set; }
    public decimal CantidadKits { get; set; }
}

/// <summary>Kit ya guardado en el presupuesto del proyecto (o null si todavía no se guardó ninguno).</summary>
public class KitProyectoGuardadoDto
{
    public int KitId { get; set; }
    public string NombreKit { get; set; } = "";
    public decimal CantidadKits { get; set; }
    public List<KitCalculoLineaDto> Lineas { get; set; } = [];
    public decimal Total { get; set; }
}
