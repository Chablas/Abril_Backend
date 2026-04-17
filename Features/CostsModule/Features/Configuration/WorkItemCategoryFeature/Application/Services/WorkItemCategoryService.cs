using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Application.Interfaces;
using Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.WorkItemCategoryFeature.Application.Services
{
    public class WorkItemCategoryService : IWorkItemCategoryService
    {
        private readonly IWorkItemCategoryRepository _repository;

        public WorkItemCategoryService(IWorkItemCategoryRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<WorkItemCategoryDto>> GetPaged(WorkItemCategoryFilterDto filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return await _repository.GetPaged(filter);
        }

        public async Task Create(WorkItemCategoryCreateDto dto, int userId)
        {
            await _repository.Create(dto, userId);
        }

        public async Task Update(WorkItemCategoryEditDto dto, int userId)
        {
            await _repository.Update(dto, userId);
        }

        public async Task<bool> Delete(int workItemCategoryId, int userId)
        {
            return await _repository.Delete(workItemCategoryId, userId);
        }
    }
}
