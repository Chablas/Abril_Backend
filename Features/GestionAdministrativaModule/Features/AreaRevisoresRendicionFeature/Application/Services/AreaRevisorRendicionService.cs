using Abril_Backend.Features.GestionAdministrativa.AreaRevisoresRendicion.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.AreaRevisoresRendicion.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisoresRendicion.Application.Services
{
    public class AreaRevisorRendicionService : IAreaRevisorRendicionService
    {
        private readonly IAreaRevisorRendicionRepository _repo;

        public AreaRevisorRendicionService(IAreaRevisorRendicionRepository repo)
        {
            _repo = repo;
        }

        public Task<AreaAsignacionInicialDto> GetInitialDataAsync(int userId, bool verTodas)
            => _repo.GetInitialDataAsync(userId, verTodas);

        public Task UpdateAreaRevisoresAsync(int areaScopeId, int? projectId, List<AreaAsignacionInputDto> revisores)
            => _repo.UpdateAreaRevisoresAsync(areaScopeId, projectId, revisores);

        public Task SetFiltroProyectoAsync(int areaScopeId, bool filtraPorProyecto, bool firmaConsolidadoPorProyecto)
            => _repo.SetFiltroProyectoAsync(areaScopeId, filtraPorProyecto, firmaConsolidadoPorProyecto);
    }
}
