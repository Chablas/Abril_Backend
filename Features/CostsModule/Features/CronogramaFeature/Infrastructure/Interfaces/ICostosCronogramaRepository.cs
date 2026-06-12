using Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Application.Dtos;

namespace Abril_Backend.Features.CostsModule.Features.CronogramaFeature.Infrastructure.Interfaces
{
    public interface ICostosCronogramaRepository
    {
        Task<List<CronogramaActividadDto>> GetActividadesAsync();
        Task<CronogramaActividadDto> CreateActividadAsync(string nombre, int userId);
        Task<List<CronogramaNodoDto>> GetNodosAsync(int projectSubContractorId);
        Task SaveAsync(int projectSubContractorId, List<CronogramaNodoDto> nodos, int userId);
    }
}
