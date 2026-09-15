using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Interfaces
{
    public interface IActivoRotativoRepository
    {
        // --- Materiales (catálogo único) ---
        Task<List<ActivoRotativoMaterialDto>> GetMaterialesAsync();
        Task<SsActivoRotativoMaterial> CreateMaterialAsync(ActivoRotativoMaterialUpsertDto dto);
        Task UpdateMaterialAsync(int materialId, ActivoRotativoMaterialUpsertDto dto);
        Task DeleteMaterialAsync(int materialId);
        Task<List<PresupuestoItemBuscarDto>> BuscarItemsPresupuestoAsync(string q);
        Task<List<ResponsableSsomaDto>> GetResponsablesSsomaAsync();

        // --- Activos ---
        Task<List<ActivoRotativoListDto>> GetActivosAsync();
        Task<ActivoRotativoDetalleDto?> GetActivoDetalleAsync(int activoId);
        Task<SsActivoRotativo> CreateActivoAsync(ActivoRotativoUpsertDto dto);
        Task UpdateActivoAsync(int activoId, ActivoRotativoUpsertDto dto);
        Task<SsActivoRotativo> MoverActivoAsync(int activoId, ActivoRotativoMoverDto dto, int? userId);
        Task DeleteActivoAsync(int activoId);
    }
}
