using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos;
using Abril_Backend.Features.CostsModule.Shared.Models;

namespace Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Infrastructure.Interfaces
{
    public interface IProjectRepository
    {
        Task<PagedResult<ProjectDto>> GetPaged(int page);
        Task Create(ProjectCreateDto dto, int userId);
        Task Update(ProjectEditDto dto, int userId);
        Task<bool> DeleteSoftAsync(int projectId, int userId);
        Task<Contributor?> FindContributorByRuc(string ruc);
        Task<Contributor> CreateContributor(string ruc, string name, string address, string economicActivity, int userId);
    }
}
