namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

/// <summary>Montos fijos "glb" tipeados a mano por proyecto — Malla Anticaída, Encapsulado y
/// Malla Anillo Fenólico no escalan con ningún driver (Área/HH/Trabajadores) ni tienen ratio
/// histórico confiable: el costo real depende de la geometría/altura del edificio de cada obra,
/// así que el responsable SSOMA los tipea directo.</summary>
public class CostoFijoManualDto
{
    public int ProjectId { get; set; }
    public decimal MallaAnticaida { get; set; }
    public decimal Encapsulado { get; set; }
    public decimal MallaAnilloFenolico { get; set; }
    public string? Notas { get; set; }
}

public class ActualizarCostoFijoManualDto
{
    public decimal MallaAnticaida { get; set; }
    public decimal Encapsulado { get; set; }
    public decimal MallaAnilloFenolico { get; set; }
    public string? Notas { get; set; }
}
