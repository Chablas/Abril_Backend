using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.MiSaludFeature.Infrastructure.Interfaces
{
    public interface IMiSaludRepository
    {
        Task<int> ResolverWorkerIdAsync(int userId);
        Task<MiSaludResumenDto> GetResumen(int workerId);
        Task<PagedResult<MiDescansoDto>> GetDescansos(int workerId, int page);
        Task<int> CreateDescanso(int workerId, CrearMiDescansoDto dto, int? userId, List<(string Url, string Nombre)> adjuntos);
        Task<DescansoNotificacionDatosDto> GetDatosNotificacionDescansoAsync(int workerId, int userId, int? motivoId);
    }
}
