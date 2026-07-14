using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Interfaces
{
    public interface IAreaRevisorService
    {
        Task<AreaRevisorInicialDto> GetInitialDataAsync();
        Task UpdateAreaRevisoresAsync(int areaScopeId, List<AreaRevisorAsignacionDto> revisores);
    }
}
