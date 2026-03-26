using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;
namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IProjectRepository
    {
        Task<List<ProjectDTO>> GetAll();
        Task<List<ProjectSimpleDTO>> GetAllFactory();
        Task<PagedResult<ProjectDTO>> GetPaged(int page);
        Task<Project> Create(ProjectCreateDTO dto, int userId);
        Task<Project> Update(ProjectEditDTO dto, int userId);
        Task<bool> DeleteSoftAsync(int projectId, int userId);
        Task<string> GetProjectNameByProjectId(int projectId);
        Task<PagedResult<ProjectDTO>> GetPagedWithResidents(int page);
    }
}