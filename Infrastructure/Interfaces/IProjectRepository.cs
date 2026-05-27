using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.ConfigurationModule.Features.ProjectFeature.Application.Dtos;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IProjectRepository
    {
        Task<List<ProjectDTO>> GetAll();
        Task<List<ProjectSimpleDTO>> GetAllFactory();
        Task<PagedResult<ProjectDTO>> GetPaged(int page, bool? activo = null);
        Task<PagedResult<ProjectDTO>> GetPagedWithResidents(int page);
        Task<string> GetProjectNameByProjectId(int projectId);
        Task UpdateFotoUrlAsync(int projectId, string? fotoUrl);
    }
}
