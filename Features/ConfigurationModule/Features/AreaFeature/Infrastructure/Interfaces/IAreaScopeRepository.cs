using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Dtos;

namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Interfaces
{
    public interface IAreaScopeRepository
    {
        Task<List<AreaScopeTreeDto>> GetTreeAsync();
        Task CreateBranchAsync(AreaScopeBranchDto dto);
        Task DeleteAsync(int areaScopeId);
    }
}
