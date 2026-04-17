using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Interfaces;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemFeature.Application.Services
{
    public class WorkItemService : IWorkItemService
    {
        private readonly IWorkItemRepository _repository;

        public WorkItemService(IWorkItemRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<WorkItemDto>> GetPaged(WorkItemFilterDto filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return await _repository.GetPaged(filter);
        }

        public async Task Create(WorkItemCreateDto dto, int userId)
        {
            await _repository.Create(dto, userId);
        }

        public async Task Update(WorkItemEditDto dto, int userId)
        {
            await _repository.Update(dto, userId);
        }

        public async Task<bool> Delete(int workItemId, int userId)
        {
            return await _repository.Delete(workItemId, userId);
        }
    }
}
