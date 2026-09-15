using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Interfaces
{
    public interface IActivoRotativoService
    {
        // Materiales
        Task<List<ActivoRotativoMaterialDto>> GetMaterialesAsync();
        Task<ActivoRotativoMaterialDto> CreateMaterialAsync(ActivoRotativoMaterialUpsertDto dto);
        Task UpdateMaterialAsync(int materialId, ActivoRotativoMaterialUpsertDto dto);
        Task DeleteMaterialAsync(int materialId);
        Task<List<PresupuestoItemBuscarDto>> BuscarItemsPresupuestoAsync(string q);
        Task<List<ResponsableSsomaDto>> GetResponsablesSsomaAsync();

        // Activos
        Task<List<ActivoRotativoListDto>> GetActivosAsync();
        Task<ActivoRotativoDetalleDto?> GetActivoDetalleAsync(int activoId);
        Task<ActivoRotativoDetalleDto> CreateActivoAsync(ActivoRotativoUpsertDto dto);
        Task UpdateActivoAsync(int activoId, ActivoRotativoUpsertDto dto);
        Task<ActivoRotativoDetalleDto> MoverActivoAsync(int activoId, ActivoRotativoMoverDto dto, int? userId);
        Task DeleteActivoAsync(int activoId);
    }
}
