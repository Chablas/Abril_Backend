using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Application.Dtos;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.ProjectLinkFeature.Application.Interfaces
{
    public interface IProjectLinkService
    {
        Task<PagedResult<ProjectLinkDto>> GetPaged(ProjectLinkFilterDto filter);
        Task<ProjectLinkFormDataDto> GetFormData();
        Task Create(ProjectLinkCreateDto dto, int userId);
        Task Update(ProjectLinkUpdateDto dto, int userId);
        Task<bool> Delete(int projectLinkId, int userId);
    }
}
