using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IUserProjectRepository
    {
        Task<object> GetPagedFactory(int page, int pageSizeQuery);
        /// <summary>
        /// Usuarios (con sus proyectos) que NO han subido lecciones en el período
        /// indicado (formato "MM-yyyy"). Filtra por user_project.state/active = true
        /// y por la ausencia de lesson(created_user_id, project_id, period, state, active).
        /// </summary>
        Task<List<UserWithoutLessonsDTO>> GetUsersWithoutLessonsThisMonth(string period);
        Task<UserProject> Create(UserProjectCreateDTO dto, int userId);
        Task<bool> DeleteSoftAsync(int userProjectId, int updatedUserId);
        Task<List<UserWithoutLessonsDTO>> GetUsersWithoutLessonsByPeriod(DateTime periodDate);
    }
}