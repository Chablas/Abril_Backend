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

        public Task<RevisorSalidaInicialDto> GetInitialDataAsync()
            => _repo.GetInitialDataAsync();

        public Task UpdateWorkerRevisoresAsync(int workerId, List<WorkerRevisorAsignacionDto> revisores)
            => _repo.UpdateWorkerRevisoresAsync(workerId, revisores);
    }
}
