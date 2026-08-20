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
    }
}
