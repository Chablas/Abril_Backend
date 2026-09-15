using Abril_Backend.Features.GestionAdministrativa.AreaConsolidadores.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.AreaConsolidadores.Infrastructure.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.AreaConsolidadores.Application.Services
{
    public class AreaConsolidadorService : IAreaConsolidadorService
    {
        private readonly IAreaConsolidadorRepository _repo;

        public AreaConsolidadorService(IAreaConsolidadorRepository repo)
        {
            _repo = repo;
        }

        public Task<AreaAsignacionInicialDto> GetInitialDataAsync(int userId, bool verTodas)
            => _repo.GetInitialDataAsync(userId, verTodas);

        public Task UpdateAreaConsolidadoresAsync(
            int areaScopeId, int? projectId, List<AreaAsignacionInputDto> consolidadores)
            => _repo.UpdateAreaConsolidadoresAsync(areaScopeId, projectId, consolidadores);

        public Task SetFiltroProyectoAsync(int areaScopeId, bool filtraPorProyecto)
            => _repo.SetFiltroProyectoAsync(areaScopeId, filtraPorProyecto);
    }
}
