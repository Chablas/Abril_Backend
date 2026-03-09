using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;
namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IProjectRepository
    {
        Task<List<ProjectScheduleSimpleDTO>> GetWithResidentByUserId(int userId);
        Task<List<ProjectDTO>> GetAll();
        Task<List<ProjectSimpleDTO>> GetAllFactory();
        Task<object> GetPaged(int page);
        Task<Project> Create(ProjectCreateDTO dto, int userId);
        Task<Project> Update(ProjectEditDTO dto, int userId);
        Task<bool> DeleteSoftAsync(int projectId, int userId);
        Task<List<ProjectSimpleDTO>> GetProjectWithResidents();
        Task<string> GetProjectNameByProjectId(int projectId);
    }
}