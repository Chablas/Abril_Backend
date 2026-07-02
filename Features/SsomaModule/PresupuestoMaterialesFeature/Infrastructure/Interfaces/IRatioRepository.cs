using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

public class RatioRawData
{
    public int FamiliaId { get; set; }
    public string NombreFamilia { get; set; } = null!;
    public string TipoMaterial { get; set; } = null!;
    public string VariableBase { get; set; } = null!;
    public decimal CantidadTotal { get; set; }
    public decimal PrecioUnitarioPromedio { get; set; }
    public decimal PrecioTotal { get; set; }
}

public interface IRatioRepository
{
    Task<List<RatioRawData>> ObtenerConsumosPorProyectoAsync(int projectId);
    Task UpsertRatioAsync(int familiaId, int projectId, string variableBase, decimal cantidadTotal,
        decimal precioUnitarioPromedio, decimal valorDriver, decimal ratioCantidad, bool esOutlier);
    Task<List<RatioProyectoDto>> ObtenerRatiosPorProyectoAsync(int projectId);
    Task<List<RatioProyectoDto>> ObtenerRatiosPorFamiliaAsync(int familiaId);
    Task<List<ResumenProyectoRatioDto>> ObtenerResumenAsync();
}
