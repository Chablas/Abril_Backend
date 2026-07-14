using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Services
{
    public class AreaRevisorService : IAreaRevisorService
    {
        private readonly IAreaRevisorRepository _repo;

        public AreaRevisorService(IAreaRevisorRepository repo)
        {
            _repo = repo;
        }

        public Task<AreaRevisorInicialDto> GetInitialDataAsync()
            => _repo.GetInitialDataAsync();

        public Task UpdateAreaRevisoresAsync(int areaScopeId, List<AreaRevisorAsignacionDto> revisores)
            => _repo.UpdateAreaRevisoresAsync(areaScopeId, revisores);
    }
}
