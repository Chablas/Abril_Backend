using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Application.Interfaces
{
    public interface IProjectService
    {
        Task<List<ProjectScheduleSimpleDTO>> GetWithResidentByUserId(int userId);
        Task<PagedResult<ProjectDTO>> GetPaged(int page);
        Task Create(ProjectCreateDTO dto, int userId);
        Task Update(ProjectEditDTO dto, int userId);
        Task<bool> DeleteSoftAsync(int projecdId, int userId);
    }
}