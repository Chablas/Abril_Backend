using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Interfaces
{
    public interface IPlantillaCronogramaRepository
    {
        Task<List<PlantillaItemDto>> GetByTipoAsync(string tipoCronograma);
        Task<PlantillaItemDto> CrearItemAsync(CrearPlantillaItemRequest request, int userId);
        Task<PlantillaItemDto> EditarItemAsync(int id, EditarPlantillaItemRequest request, int userId);
        Task EliminarItemAsync(int id, int userId);
    }
}
