using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisoresRendicion.Infrastructure.Interfaces
{
    public interface IAreaRevisorRendicionRepository
    {
        Task<AreaAsignacionInicialDto> GetInitialDataAsync(int userId, bool verTodas);

        Task UpdateAreaRevisoresAsync(int areaScopeId, int? projectId, List<AreaAsignacionInputDto> revisores);

        Task SetFiltroProyectoAsync(int areaScopeId, bool filtraPorProyecto);
    }
}
