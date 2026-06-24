using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.MiSaludFeature.Application.Interfaces
{
    public interface IMiSaludService
    {
        Task<MiSaludResumenDto> GetResumen(int userId);
        Task<PagedResult<MiDescansoDto>> GetDescansos(int userId, int page);
        Task<int> CreateDescanso(int userId, CrearMiDescansoDto dto);
    }
}
