using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Application.Interfaces;
using Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Application.Services
{
    public class ProjectLinkService : IProjectLinkService
    {
        private readonly IProjectLinkRepository _repository;

        public ProjectLinkService(IProjectLinkRepository repository)
        {
            _repository = repository;
        }

        public async Task<PagedResult<ProjectLinkDto>> GetPaged(ProjectLinkFilterDto filter)
        {
            if (filter.Page < 1) filter.Page = 1;
            return await _repository.GetPaged(filter);
        }

        public async Task<ProjectLinkFormDataDto> GetFormData()
        {
            return await _repository.GetFormData();
        }

        public async Task Create(ProjectLinkCreateDto dto, int userId)
        {
            await _repository.Create(dto, userId);
        }

        public async Task Update(ProjectLinkUpdateDto dto, int userId)
        {
            await _repository.Update(dto, userId);
        }

        public async Task<bool> Delete(int projectLinkId, int userId)
        {
            return await _repository.Delete(projectLinkId, userId);
        }
    }
}
