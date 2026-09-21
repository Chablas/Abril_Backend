using Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.EppFeature.Application.Interfaces
{
    public interface IEppService
    {
        Task<List<EppCategoriaDto>> GetCategoriasAsync();
        Task<EppCategoriaDto> CreateCategoriaAsync(EppCategoriaUpsertDto dto);
        Task UpdateCategoriaAsync(int categoriaId, EppCategoriaUpsertDto dto);
        Task SetCategoriaActivoAsync(int categoriaId, bool activo);
        Task DeleteCategoriaAsync(int categoriaId);

        Task<List<EppFamiliaDto>> GetFamiliasAsync();
        Task<EppFamiliaDto> CreateFamiliaAsync(EppFamiliaUpsertDto dto);
        Task UpdateFamiliaAsync(int familiaId, EppFamiliaUpsertDto dto);
        Task SetFamiliaActivoAsync(int familiaId, bool activo);
        Task DeleteFamiliaAsync(int familiaId);

        Task<List<EppItemListDto>> GetItemsAsync();
        Task<EppItemDetalleDto?> GetItemDetalleAsync(int itemId);
        Task<EppItemDetalleDto> CreateItemAsync(EppItemUpsertDto dto, int? userId);
        Task UpdateItemAsync(int itemId, EppItemUpsertDto dto, int? userId);
        Task SetItemActivoAsync(int itemId, bool activo, int? userId);
        Task<string> SubirImagenItemAsync(int itemId, Microsoft.AspNetCore.Http.IFormFile archivo, int? userId);
        Task<string> SubirFichaTecnicaAsync(int itemId, Microsoft.AspNetCore.Http.IFormFile archivo, int? userId);
        Task QuitarFichaTecnicaAsync(int itemId, int? userId);

        Task<EppItemDetalleDto> CreateModeloAsync(int itemId, EppModeloUpsertDto dto, int? userId);
        Task UpdateModeloAsync(int modeloId, EppModeloUpsertDto dto, int? userId);
        Task SetModeloActivoAsync(int modeloId, bool activo, int? userId);
        Task<string> SubirImagenModeloAsync(int modeloId, Microsoft.AspNetCore.Http.IFormFile archivo, int? userId);
    }
}
