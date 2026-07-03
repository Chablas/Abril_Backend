using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Interfaces
{
    public interface IRevisorSalidaService
    {
        Task<List<WorkerRevisorSalidaItemDto>> GetWorkerRevisoresAsync();
        Task<List<WorkerRevisorSalidaOptionDto>> GetWorkerRevisorOptionsAsync();
        Task UpdateWorkerRevisorAsync(int workerId, int? jefeWorkerId);
    }
}
