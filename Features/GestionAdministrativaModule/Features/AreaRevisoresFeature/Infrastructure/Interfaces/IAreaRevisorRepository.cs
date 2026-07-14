using Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisores.Infrastructure.Interfaces
{
    public interface IAreaRevisorRepository
    {
        /// <param name="userId">Usuario autenticado (app_user).</param>
        /// <param name="verTodas">true = ve todas las áreas (rol ADMINISTRADOR DE SOLICITUD DE SALIDAS).</param>
        Task<AreaRevisorInicialDto> GetInitialDataAsync(int userId, bool verTodas);
        Task UpdateAreaRevisoresAsync(int areaScopeId, List<AreaRevisorAsignacionDto> revisores);
    }
}
