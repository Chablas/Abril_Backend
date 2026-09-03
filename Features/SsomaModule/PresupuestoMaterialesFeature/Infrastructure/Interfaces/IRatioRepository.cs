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

public class RatioUpsertItem
{
    public int FamiliaId { get; set; }
    public int ProjectId { get; set; }
    public string VariableBase { get; set; } = null!;
    public decimal CantidadTotal { get; set; }
    public decimal PrecioUnitarioPromedio { get; set; }
    public decimal ValorDriver { get; set; }
    public decimal RatioCantidad { get; set; }
}

public interface IRatioRepository
{
    Task<List<RatioRawData>> ObtenerConsumosPorProyectoAsync(int projectId);
    /// <summary>Proyectos que tienen al menos una línea de consumo SSOMA ya estandarizada — candidatos para "Calcular ratios de todos".</summary>
    Task<List<(int ProjectId, string ProjectDescription)>> ObtenerProyectosConConsumoEstandarizadoAsync();
    Task UpsertRatiosBulkAsync(List<RatioUpsertItem> items);
    /// <summary>Borra las filas de ss_ratio_proyecto de un proyecto cuya familia ya no tiene consumo
    /// vigente (ej. se fusionó/movió a otro ítem) — sin esto, un recálculo solo hace UPSERT y nunca
    /// limpia las familias que dejaron de aplicar, quedando "fantasmas" con el último valor calculado.</summary>
    Task EliminarRatiosObsoletosAsync(int projectId, List<int> familiaIdsVigentes);
    Task<List<RatioProyectoDto>> ObtenerRatiosPorProyectoAsync(int projectId);
    Task<List<RatioProyectoDto>> ObtenerRatiosPorFamiliaAsync(int familiaId);
    Task ActualizarIncluidoManualAsync(int familiaId, int projectId, bool incluir, string campo);
    /// <summary>Activa/desactiva una familia directamente desde la pantalla de Ratios — mismo flag
    /// que el toggle "Activo" de Catálogo, sin tener que reenviar el DTO completo de la familia.</summary>
    Task ActualizarActivoFamiliaAsync(int familiaId, bool activo);
    Task<List<FamiliaConRatioDto>> ListarFamiliasConRatioAsync();
    Task<List<ResumenProyectoRatioDto>> ObtenerResumenAsync();
}
