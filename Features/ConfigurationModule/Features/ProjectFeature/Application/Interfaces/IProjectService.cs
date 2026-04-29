using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos;

namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Interfaces
{
    public interface IProjectService
    {
        Task<PagedResult<ProjectDto>> GetPaged(int page);
        Task Create(ProjectCreateDto dto, int userId);
        Task Update(ProjectEditDto dto, int userId);
        Task<bool> DeleteSoftAsync(int projectId, int userId);
        Task<ContributorLookupDto?> GetOrCreateCompanyByRuc(string ruc, int userId);
        Task UpdateEmails(int id, ProjectEmailsUpdateDto dto);
    }
}
