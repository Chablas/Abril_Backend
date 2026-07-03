using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Services
{
    public class RevisorSalidaService : IRevisorSalidaService
    {
        private readonly IRevisorSalidaRepository _repo;

        public RevisorSalidaService(IRevisorSalidaRepository repo)
        {
            _repo = repo;
        }

        public Task<List<WorkerRevisorSalidaItemDto>> GetWorkerRevisoresAsync()
            => _repo.GetWorkerRevisoresAsync();

        public Task<List<WorkerRevisorSalidaOptionDto>> GetWorkerRevisorOptionsAsync()
            => _repo.GetWorkerRevisorOptionsAsync();

        public Task UpdateWorkerRevisorAsync(int workerId, int? jefeWorkerId)
            => _repo.UpdateWorkerRevisorAsync(workerId, jefeWorkerId);
    }
}
