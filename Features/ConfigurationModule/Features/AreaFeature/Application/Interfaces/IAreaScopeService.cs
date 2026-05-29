using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Dtos;

namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Interfaces
{
    public interface IAreaScopeService
    {
        Task<List<AreaScopeTreeDto>> GetTreeAsync();
        Task CreateBranchAsync(AreaScopeBranchDto dto);
        Task DeleteAsync(int areaScopeId);
    }
}
