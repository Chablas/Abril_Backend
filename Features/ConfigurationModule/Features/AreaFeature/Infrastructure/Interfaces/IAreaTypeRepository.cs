using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Application.Dtos;

namespace Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Interfaces
{
    public interface IAreaTypeRepository
    {
        Task<PagedResult<AreaTypeDto>> GetPaged(AreaTypeFilterDto filter);
        Task<List<AreaTypeSimpleDto>> GetSimple();
        Task Create(AreaTypeCreateDto dto);
        Task Update(AreaTypeEditDto dto);
        Task<bool> DeleteSoftAsync(int areaTypeId);
    }
}
