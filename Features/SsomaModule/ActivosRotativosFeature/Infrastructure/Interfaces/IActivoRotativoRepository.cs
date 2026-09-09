using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ActivosRotativosFeature.Infrastructure.Interfaces
{
    public interface IActivoRotativoRepository
    {
        // --- Categorías ---
        Task<List<ActivoRotativoCategoriaDto>> GetCategoriasAsync();
        Task<SsActivoRotativoCategoria> CreateCategoriaAsync(ActivoRotativoCategoriaUpsertDto dto);
        Task UpdateCategoriaAsync(int categoriaId, ActivoRotativoCategoriaUpsertDto dto);

        // --- Activos ---
        Task<List<ActivoRotativoListDto>> GetActivosAsync();
        Task<ActivoRotativoDetalleDto?> GetActivoDetalleAsync(int activoId);
        Task<SsActivoRotativo> CreateActivoAsync(ActivoRotativoUpsertDto dto);
        Task UpdateActivoAsync(int activoId, ActivoRotativoUpsertDto dto);
        Task<SsActivoRotativo> MoverActivoAsync(int activoId, ActivoRotativoMoverDto dto, int? userId);
    }
}
