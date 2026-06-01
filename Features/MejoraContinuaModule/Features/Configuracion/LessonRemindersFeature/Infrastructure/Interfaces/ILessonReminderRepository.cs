using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Application.Dtos;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Infrastructure.Interfaces
{
    public interface ILessonReminderRepository
    {
        Task<object> GetPaged(int page, int pageSize);
        Task<LessonReminderCreateDataDTO> GetCreateData();
        Task Create(LessonReminderCreateDTO dto, int userId);
        Task<bool> DeleteSoftAsync(int userProjectId, int userId);
        Task<ToggleLessonReminderResultDTO> ToggleActiveAsync(int userProjectId, int userId);

        // Filtro de staff_email por proyecto
        Task<List<ProjectStaffReminderConfigItemDTO>> GetAllProjectStaffAsync();
        Task<ToggleProjectStaffReminderResultDTO> ToggleProjectStaffAsync(int projectId);
        Task<List<ActiveProjectStaffEmailDTO>> GetActiveStaffEmailsAsync();
    }
}
