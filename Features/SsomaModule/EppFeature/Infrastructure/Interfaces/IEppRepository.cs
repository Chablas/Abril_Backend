using Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Interfaces
{
    public interface IEppRepository
    {
        // Categorías
        Task<List<EppCategoriaDto>> GetCategoriasAsync();
        Task<SsEppCategoria> CreateCategoriaAsync(EppCategoriaUpsertDto dto);
        Task UpdateCategoriaAsync(int categoriaId, EppCategoriaUpsertDto dto);
        Task SetCategoriaActivoAsync(int categoriaId, bool activo);
        Task DeleteCategoriaAsync(int categoriaId);

        // Familias
        Task<List<EppFamiliaDto>> GetFamiliasAsync();
        Task<SsEppFamilia> CreateFamiliaAsync(EppFamiliaUpsertDto dto);
        Task UpdateFamiliaAsync(int familiaId, EppFamiliaUpsertDto dto);
        Task SetFamiliaActivoAsync(int familiaId, bool activo);
        Task DeleteFamiliaAsync(int familiaId);

        // Ítems
        Task<List<EppItemListDto>> GetItemsAsync();
        Task<EppItemDetalleDto?> GetItemDetalleAsync(int itemId);
        Task<SsEppItem> CreateItemAsync(EppItemUpsertDto dto, int? userId);
        Task UpdateItemAsync(int itemId, EppItemUpsertDto dto, int? userId);
        Task SetItemActivoAsync(int itemId, bool activo, int? userId);
        Task SetItemImagenAsync(int itemId, string imagenUrl, int? userId);
        Task SetItemFichaTecnicaAsync(int itemId, string fichaTecnicaUrl, string nombreArchivo, int? userId);
        Task QuitarFichaTecnicaAsync(int itemId, int? userId);

        // Modelos
        Task<SsEppModelo> CreateModeloAsync(int itemId, EppModeloUpsertDto dto, int? userId);
        Task UpdateModeloAsync(int modeloId, EppModeloUpsertDto dto, int? userId);
        Task SetModeloActivoAsync(int modeloId, bool activo, int? userId);
        Task SetModeloImagenAsync(int modeloId, string imagenUrl, int? userId);
    }
}
