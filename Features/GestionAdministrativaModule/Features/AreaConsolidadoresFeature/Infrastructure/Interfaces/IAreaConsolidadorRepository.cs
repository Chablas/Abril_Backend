using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.AreaConsolidadores.Infrastructure.Interfaces
{
    public interface IAreaConsolidadorRepository
    {
        /// <param name="userId">Usuario autenticado (app_user).</param>
        /// <param name="verTodas">true = ve todas las áreas y puede editarlas.</param>
        Task<AreaAsignacionInicialDto> GetInitialDataAsync(int userId, bool verTodas);

        /// <param name="projectId">null = a nivel de área; con valor = de ese proyecto dentro del área.</param>
        Task UpdateAreaConsolidadoresAsync(int areaScopeId, int? projectId, List<AreaAsignacionInputDto> consolidadores);

        /// <summary>Marca/desmarca "filtrar por proyecto" para el área (upsert en ga_salidas_area_config).</summary>
        Task SetFiltroProyectoAsync(int areaScopeId, bool filtraPorProyecto);
    }
}
