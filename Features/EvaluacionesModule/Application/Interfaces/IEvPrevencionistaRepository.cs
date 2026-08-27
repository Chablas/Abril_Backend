using Abril_Backend.Features.Evaluaciones.Application.Dtos;
using Abril_Backend.Features.Evaluaciones.Infrastructure.Models;

namespace Abril_Backend.Features.Evaluaciones.Application.Interfaces
{
    public interface IEvPrevencionistaRepository
    {
        Task<EvPrevencionistaInicioDto> GetInicioAsync(int evaluadorUserId, int evaluadorContributorId, List<int> proyectoIds);
        Task<int?> ResolverEvaluadorSsUsuarioIdAsync(int userId, int contributorId);
        Task<EvEvaluacionPrevencionista> CreateAsync(
            EvEvaluacionPrevencionista eval, List<EvEvaluacionPrevencionistaDetalle> detalles);
        Task<bool> ExisteAsync(int periodoId, int evaluadoUserId, int proyectoId, int evaluadorSsContratistaUsuarioId);
        Task<EvPrevencionistaMiPerfilDto> GetMiPerfilAsync(int evaluadoUserId, int? periodoId);
        Task<EvPrevencionistaDashboardDto> GetDashboardAsync(int? periodoId, int? proyectoId);

        /// <summary>
        /// Supervisores de campo de contratista (ss_contratista_usuario activos) con al
        /// menos un proyecto asignado — pool para el recordatorio consolidado. El alcance
        /// de proyecto normalmente sale del JWT (claim proyectoIds); acá se reconstruye
        /// desde ss_contratista_usuario_proyecto porque el cron no tiene ese token.
        /// </summary>
        Task<List<EvPrevencionistaCandidatoDto>> GetEvaluadoresCandidatosAsync();
    }
}
