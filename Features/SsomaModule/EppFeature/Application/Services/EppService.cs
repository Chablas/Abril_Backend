using Abril_Backend.Features.SsomaModule.EppFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.EppFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.EppFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.SsomaModule.EppFeature.Application.Services
{
    public class EppService : IEppService
    {
        private readonly IEppRepository _repository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IStorageContainerResolver _containerResolver;

        public EppService(
            IEppRepository repository,
            IFileStorageService fileStorageService,
            IStorageContainerResolver containerResolver)
        {
            _repository = repository;
            _fileStorageService = fileStorageService;
            _containerResolver = containerResolver;
        }

        public Task<List<EppCategoriaDto>> GetCategoriasAsync() => _repository.GetCategoriasAsync();

        public async Task<EppCategoriaDto> CreateCategoriaAsync(EppCategoriaUpsertDto dto)
        {
            var entity = await _repository.CreateCategoriaAsync(dto);
            return new EppCategoriaDto { Id = entity.Id, Nombre = entity.Nombre, Orden = entity.Orden, Activo = entity.Activo, TotalItems = 0 };
        }

        public Task UpdateCategoriaAsync(int categoriaId, EppCategoriaUpsertDto dto) => _repository.UpdateCategoriaAsync(categoriaId, dto);

        public Task SetCategoriaActivoAsync(int categoriaId, bool activo) => _repository.SetCategoriaActivoAsync(categoriaId, activo);

        public Task DeleteCategoriaAsync(int categoriaId) => _repository.DeleteCategoriaAsync(categoriaId);

        public Task<List<EppFamiliaDto>> GetFamiliasAsync() => _repository.GetFamiliasAsync();

        public async Task<EppFamiliaDto> CreateFamiliaAsync(EppFamiliaUpsertDto dto)
        {
            var entity = await _repository.CreateFamiliaAsync(dto);
            return new EppFamiliaDto { Id = entity.Id, Nombre = entity.Nombre, CategoriaId = entity.CategoriaId, Orden = entity.Orden, Activo = entity.Activo, TotalItems = 0 };
        }

        public Task UpdateFamiliaAsync(int familiaId, EppFamiliaUpsertDto dto) => _repository.UpdateFamiliaAsync(familiaId, dto);

        public Task SetFamiliaActivoAsync(int familiaId, bool activo) => _repository.SetFamiliaActivoAsync(familiaId, activo);

        public Task DeleteFamiliaAsync(int familiaId) => _repository.DeleteFamiliaAsync(familiaId);

        public Task<List<EppItemListDto>> GetItemsAsync() => _repository.GetItemsAsync();

        public Task<EppItemDetalleDto?> GetItemDetalleAsync(int itemId) => _repository.GetItemDetalleAsync(itemId);

        public async Task<EppItemDetalleDto> CreateItemAsync(EppItemUpsertDto dto, int? userId)
        {
            var entity = await _repository.CreateItemAsync(dto, userId);
            return await _repository.GetItemDetalleAsync(entity.Id)
                ?? throw new InvalidOperationException("No se pudo recuperar el ítem recién creado.");
        }

        public Task UpdateItemAsync(int itemId, EppItemUpsertDto dto, int? userId) => _repository.UpdateItemAsync(itemId, dto, userId);

        public Task SetItemActivoAsync(int itemId, bool activo, int? userId) => _repository.SetItemActivoAsync(itemId, activo, userId);

        public async Task<string> SubirImagenItemAsync(int itemId, IFormFile archivo, int? userId)
        {
            var url = await SubirImagenAsync(archivo);
            await _repository.SetItemImagenAsync(itemId, url, userId);
            return url;
        }

        public async Task<string> SubirFichaTecnicaAsync(int itemId, IFormFile archivo, int? userId)
        {
            if (archivo.Length == 0) throw new InvalidOperationException("El archivo está vacío.");
            if (!string.Equals(Path.GetExtension(archivo.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La ficha técnica debe ser un archivo PDF.");

            var container = _containerResolver.GetEppFichasTecnicasContainerName();
            var fileName = $"{Guid.NewGuid()}.pdf";

            await using var stream = archivo.OpenReadStream();
            var uploadedUrls = await _fileStorageService.UploadFilesAsync(
                new[] { (Stream: (Stream)stream, FileName: fileName) }, container);

            var url = uploadedUrls.First();
            await _repository.SetItemFichaTecnicaAsync(itemId, url, archivo.FileName, userId);
            return url;
        }

        public Task QuitarFichaTecnicaAsync(int itemId, int? userId) => _repository.QuitarFichaTecnicaAsync(itemId, userId);

        public async Task<EppItemDetalleDto> CreateModeloAsync(int itemId, EppModeloUpsertDto dto, int? userId)
        {
            await _repository.CreateModeloAsync(itemId, dto, userId);
            return await _repository.GetItemDetalleAsync(itemId)
                ?? throw new InvalidOperationException("No se pudo recuperar el ítem.");
        }

        public Task UpdateModeloAsync(int modeloId, EppModeloUpsertDto dto, int? userId) => _repository.UpdateModeloAsync(modeloId, dto, userId);

        public Task SetModeloActivoAsync(int modeloId, bool activo, int? userId) => _repository.SetModeloActivoAsync(modeloId, activo, userId);

        public async Task<string> SubirImagenModeloAsync(int modeloId, IFormFile archivo, int? userId)
        {
            var url = await SubirImagenAsync(archivo);
            await _repository.SetModeloImagenAsync(modeloId, url, userId);
            return url;
        }

        private async Task<string> SubirImagenAsync(IFormFile archivo)
        {
            if (archivo.Length == 0) throw new InvalidOperationException("El archivo está vacío.");

            var container = _containerResolver.GetEppImagenesContainerName();
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";

            await using var stream = archivo.OpenReadStream();
            var uploadedUrls = await _fileStorageService.UploadFilesAsync(
                new[] { (Stream: (Stream)stream, FileName: fileName) }, container);

            return uploadedUrls.First();
        }
    }
}
