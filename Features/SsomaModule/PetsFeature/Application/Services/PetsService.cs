using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.PetsFeature.Application.Services;

public class PetsService : IPetsService
{
    private const string ContainerName = "ssoma-pets-pasos";

    private readonly IPetsRepository _repo;
    private readonly IFileStorageService _storage;

    public PetsService(IPetsRepository repo, IFileStorageService storage)
    {
        _repo = repo;
        _storage = storage;
    }

    public Task<List<PetListItemDto>> GetListAsync() => _repo.GetListAsync();

    public async Task<PetDetalleDto> GetDetalleAsync(int id)
        => await _repo.GetDetalleAsync(id) ?? throw new AbrilException("PETS no encontrado.", 404);

    public Task<List<PetPasoDto>> GetPasosAsync(int petId) => _repo.GetPasosAsync(petId);

    public Task<int> CrearAsync(CrearPetRequest request) => _repo.CrearAsync(request);

    public Task ActualizarAsync(int id, ActualizarPetRequest request) => _repo.ActualizarAsync(id, request);

    public Task<int> AgregarPasoAsync(int petId, CrearPetPasoRequest request) => _repo.AgregarPasoAsync(petId, request);

    public Task ActualizarPasoAsync(int petId, int pasoId, ActualizarPetPasoRequest request)
        => _repo.ActualizarPasoAsync(petId, pasoId, request);

    public Task EliminarPasoAsync(int petId, int pasoId) => _repo.EliminarPasoAsync(petId, pasoId);

    public Task ReordenarPasosAsync(int petId, ReordenarPasosRequest request) => _repo.ReordenarPasosAsync(petId, request);

    public async Task<string> SubirImagenPasoAsync(int petId, int pasoId, Stream fileStream, string fileName)
    {
        var urls = await _storage.UploadFilesAsync([(fileStream, fileName)], ContainerName);
        var url = urls.FirstOrDefault()
            ?? throw new AbrilException("No se pudo subir la imagen.", 500);

        await _repo.SetImagenPasoAsync(petId, pasoId, url);
        return url;
    }
}
