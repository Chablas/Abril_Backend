using Abril_Backend.Features.SsomaModule.PetsFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PetsFeature.Application.Interfaces;

public interface IPetsService
{
    Task<List<PetListItemDto>> GetListAsync();
    Task<PetDetalleDto> GetDetalleAsync(int id);
    Task<List<PetPasoDto>> GetPasosAsync(int petId);
    Task<int> CrearAsync(CrearPetRequest request);
    Task ActualizarAsync(int id, ActualizarPetRequest request);
    Task<int> AgregarPasoAsync(int petId, CrearPetPasoRequest request);
    Task ActualizarPasoAsync(int petId, int pasoId, ActualizarPetPasoRequest request);
    Task EliminarPasoAsync(int petId, int pasoId);
    Task ReordenarPasosAsync(int petId, ReordenarPasosRequest request);
    Task<string> SubirImagenPasoAsync(int petId, int pasoId, Stream fileStream, string fileName);

    // Catálogo (Marco Legal / EPP / Recursos)
    Task<List<CatalogoItemDto>> GetCatalogoAsync(string grupo, string? tipo);
    Task<int> CrearCatalogoItemAsync(CrearCatalogoItemRequest request);
    Task DesactivarCatalogoItemAsync(int catalogoItemId);
    Task<int> SeleccionarCatalogoItemAsync(int petId, SeleccionarItemCatalogoRequest request);
    Task<int> AgregarItemPersonalizadoAsync(int petId, AgregarItemPersonalizadoRequest request);
    Task EliminarSeleccionAsync(int petId, int seleccionId);

    // Anexos
    Task<string> SubirAnexoAsync(int petId, string nombre, Stream fileStream, string fileName);
    Task EliminarAnexoAsync(int petId, int anexoId);
}
