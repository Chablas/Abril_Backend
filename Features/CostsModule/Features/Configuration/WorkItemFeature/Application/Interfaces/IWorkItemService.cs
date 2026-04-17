using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Interfaces
{
    public interface IWorkItemService
    {
        Task<PagedResult<WorkItemDto>> GetPaged(WorkItemFilterDto filter);
        Task Create(WorkItemCreateDto dto, int userId);
        Task Update(WorkItemEditDto dto, int userId);
        Task<bool> Delete(int workItemId, int userId);
    }
}
