using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Interfaces
{
    public interface IActivoRotativoService
    {
        // Categorías
        Task<List<ActivoRotativoCategoriaDto>> GetCategoriasAsync();
        Task<ActivoRotativoCategoriaDto> CreateCategoriaAsync(ActivoRotativoCategoriaUpsertDto dto);
        Task UpdateCategoriaAsync(int categoriaId, ActivoRotativoCategoriaUpsertDto dto);

        // Activos
        Task<List<ActivoRotativoListDto>> GetActivosAsync();
        Task<ActivoRotativoDetalleDto?> GetActivoDetalleAsync(int activoId);
        Task<ActivoRotativoDetalleDto> CreateActivoAsync(ActivoRotativoUpsertDto dto);
        Task UpdateActivoAsync(int activoId, ActivoRotativoUpsertDto dto);
        Task<ActivoRotativoDetalleDto> MoverActivoAsync(int activoId, ActivoRotativoMoverDto dto, int? userId);
    }
}
