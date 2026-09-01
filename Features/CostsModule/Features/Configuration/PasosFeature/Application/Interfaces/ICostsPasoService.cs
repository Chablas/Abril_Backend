using Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Application.Dtos;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Application.Interfaces
{
    public interface ICostsPasoService
    {
        Task<List<CostsPasoDto>> GetPasosAsync();
        Task UpdateOptionAsync(CostsPasoOptionUpdateDto dto, int userId);
    }
}
