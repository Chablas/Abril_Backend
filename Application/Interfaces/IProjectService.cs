using Abril_Backend.Application.DTOs;

namespace Abril_Backend.Application.Interfaces
{
    public interface IProjectService
    {
        Task<List<ProjectScheduleSimpleDTO>> GetWithResidentByUserId(int userId);
    }
}