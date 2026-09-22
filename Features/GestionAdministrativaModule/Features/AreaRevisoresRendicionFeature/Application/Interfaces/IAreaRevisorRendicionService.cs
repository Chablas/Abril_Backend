using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.AreaRevisoresRendicion.Application.Interfaces
{
    /// <summary>
    /// Aprobadores de la PRIMERA REVISIÓN y firmantes del CONSOLIDADO, por área.
    ///
    /// Es la pantalla gemela de Revisores de Áreas (Solicitud de Salidas), que desde el 2026-09-21
    /// quedó solo para aprobar la salida. Comparte con ella los DTOs de <c>AreaAsignacionDtos</c>;
    /// lo único suyo son las dos casillas por persona (primera revisión / consolidado), que aquí sí
    /// se usan porque un área en obra lleva varios aprobadores y no todos intervienen en los dos
    /// pasos.
    /// </summary>
    public interface IAreaRevisorRendicionService
    {
        /// <param name="userId">Usuario autenticado (app_user).</param>
        /// <param name="verTodas">true = ve todas las áreas y puede editarlas.</param>
        Task<AreaAsignacionInicialDto> GetInitialDataAsync(int userId, bool verTodas);

        /// <param name="projectId">null = a nivel de área; con valor = solo para ese proyecto del área.</param>
        Task UpdateAreaRevisoresAsync(int areaScopeId, int? projectId, List<AreaAsignacionInputDto> revisores);

        /// <summary>Marca/desmarca "filtrar por proyecto" para el área.</summary>
        Task SetFiltroProyectoAsync(int areaScopeId, bool filtraPorProyecto);
    }
}
