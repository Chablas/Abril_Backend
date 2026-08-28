using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PetsFeature.Infrastructure.Interfaces;

public interface IPetsRepository
{
    Task<List<PetListItemDto>> GetListAsync();
    Task<PetDetalleDto?> GetDetalleAsync(int id);
    Task<List<PetPasoDto>> GetPasosAsync(int petId);
    Task<int> CrearAsync(CrearPetRequest request);
    Task ActualizarAsync(int id, ActualizarPetRequest request);
    Task<int> AgregarPasoAsync(int petId, CrearPetPasoRequest request);
    Task ActualizarPasoAsync(int petId, int pasoId, ActualizarPetPasoRequest request);
    Task EliminarPasoAsync(int petId, int pasoId);
    Task ReordenarPasosAsync(int petId, ReordenarPasosRequest request);
    Task SetImagenPasoAsync(int petId, int pasoId, string? imagenUrl);
}
