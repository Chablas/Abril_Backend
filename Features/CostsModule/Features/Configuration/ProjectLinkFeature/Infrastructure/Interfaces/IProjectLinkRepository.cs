using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Infrastructure.Models;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Infrastructure.Interfaces
{
    public interface IProjectLinkRepository
    {
        Task<PagedResult<ProjectLinkDto>> GetPaged(ProjectLinkFilterDto filter);
        Task<ProjectLinkFormDataDto> GetFormData();
        Task<List<ProjectLink>> GetByProjectIdAsync(int projectId);
        Task Create(ProjectLinkCreateDto dto, int userId);
        Task Update(ProjectLinkUpdateDto dto, int userId);
        Task<bool> Delete(int projectLinkId, int userId);
    }
}
