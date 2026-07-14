using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Infrastructure.Interfaces
{
    public interface IAreaRevisorRepository
    {
        Task<AreaRevisorInicialDto> GetInitialDataAsync();
        Task UpdateAreaRevisoresAsync(int areaScopeId, List<AreaRevisorAsignacionDto> revisores);
    }
}
