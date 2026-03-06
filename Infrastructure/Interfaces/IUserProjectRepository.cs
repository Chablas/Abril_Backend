using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IUserProjectRepository
    {
        Task<object> GetPagedFactory(int page, int pageSizeQuery);
        Task<List<UserWithoutLessonsDTO>> GetUsersWithoutLessonsThisMonth();
        Task<UserProject> Create(UserProjectCreateDTO dto, int userId);
        Task<bool> DeleteSoftAsync(int userProjectId, int updatedUserId);
        Task<List<UserWithoutLessonsDTO>> GetUsersWithoutLessonsByPeriod(DateTime periodDate);
    }
}