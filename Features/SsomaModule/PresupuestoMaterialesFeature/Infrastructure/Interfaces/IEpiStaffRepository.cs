using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

public interface IEpiStaffRepository
{
    Task<EpiStaffConfigDto> ObtenerConfigAsync();
    Task ActualizarConfigAsync(ActualizarEpiStaffConfigDto dto);

    /// <summary>(último hito del cronograma vigente − primer hito) / 30.44 días. 0 si el
    /// proyecto no tiene cronograma vigente con hitos críticos cargados.</summary>
    Task<decimal> ObtenerMesesProyectoAsync(int projectId);
    Task<decimal> ObtenerAreaTechadaAsync(int projectId);

    /// <summary>Precio promedio (Staff, Obrero) de casco — Staff = líneas BLANCO/INGENIER,
    /// Obrero = el resto (colores). Global entre proyectos (igual criterio que
    /// ObtenerTarifasSugeridasAsync de Personal): es el precio de mercado actual del SKU, no algo
    /// que deba variar por proyecto.</summary>
    Task<(decimal PrecioStaff, decimal PrecioObrero)> ObtenerPreciosCascoAsync();

    /// <summary>Igual que ObtenerPreciosCascoAsync, para orejera — Staff = líneas "3M", Obrero =
    /// el resto (Clute/Tipo Copa).</summary>
    Task<(decimal PrecioStaff, decimal PrecioObrero)> ObtenerPreciosOrejeraAsync();
}
