using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.AreaConsolidadores.Application.Interfaces
{
    /// <summary>
    /// Consolidadores por área: quién, además del propio trabajador, puede adjuntar el Consolidado
    /// del S10 de sus planillas. Misma pantalla y mismos DTOs que Revisores de Áreas
    /// (<c>AreaAsignacionDtos</c>); la diferencia está en la lectura — acá quedan vigentes TODOS
    /// los activos, no solo el primero.
    /// </summary>
    public interface IAreaConsolidadorService
    {
        /// <param name="userId">Usuario autenticado (app_user).</param>
        /// <param name="verTodas">true = ve todas las áreas y puede editarlas.</param>
        Task<AreaAsignacionInicialDto> GetInitialDataAsync(int userId, bool verTodas);

        /// <param name="projectId">null = a nivel de área; con valor = de ese proyecto dentro del área.</param>
        Task UpdateAreaConsolidadoresAsync(int areaScopeId, int? projectId, List<AreaAsignacionInputDto> consolidadores);

        /// <summary>Marca/desmarca "filtrar por proyecto" para el área.</summary>
        Task SetFiltroProyectoAsync(int areaScopeId, bool filtraPorProyecto);
    }
}
