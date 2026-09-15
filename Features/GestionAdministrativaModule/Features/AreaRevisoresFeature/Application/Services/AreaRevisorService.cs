using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Services
{
    public class AreaRevisorService : IAreaRevisorService
    {
        private readonly IAreaRevisorRepository _repo;

        public AreaRevisorService(IAreaRevisorRepository repo)
        {
            _repo = repo;
        }

        public Task<AreaAsignacionInicialDto> GetInitialDataAsync(int userId, bool verTodas)
            => _repo.GetInitialDataAsync(userId, verTodas);

        public Task UpdateAreaRevisoresAsync(int areaScopeId, int? projectId, List<AreaAsignacionInputDto> revisores)
            => _repo.UpdateAreaRevisoresAsync(areaScopeId, projectId, revisores);

        public Task SetFiltroProyectoAsync(int areaScopeId, bool filtraPorProyecto)
            => _repo.SetFiltroProyectoAsync(areaScopeId, filtraPorProyecto);
    }
}
