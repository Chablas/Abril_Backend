namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

/// <summary>Família de Catálogo con VariableBase = FIJO (ej. alquiler de baños químicos, letrero de
/// obra) — no escala con HH/Área/Trabajadores como los materiales, así que su cantidad para el
/// proyecto se tipea a mano. El precio unitario sí sigue viniendo de Ratios (igual que Vigilancia),
/// snapshot al momento de guardar.</summary>
public class FamiliaFijaDisponibleDto
{
    public int FamiliaId { get; set; }
    public string NombreFamilia { get; set; } = "";
    public string? UnidadMedida { get; set; }
}

public class ServicioFijoDto
{
    public int FamiliaId { get; set; }
    public string NombreFamilia { get; set; } = "";
    public string? UnidadMedida { get; set; }
    public decimal Metrado { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }
    public string? Descripcion { get; set; }
}

public class ServicioFijoItemInputDto
{
    public int FamiliaId { get; set; }
    public decimal Metrado { get; set; }
    public string? Descripcion { get; set; }
}

public class ServiciosFijosGuardarDto
{
    public List<ServicioFijoItemInputDto> Items { get; set; } = [];
}
