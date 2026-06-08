using Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Application.Dtos;

namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Application.Interfaces
{
    public interface ILessonReminderService
    {
        Task<object> GetPaged(int page, int pageSize, string? subarea = null);
        Task<LessonReminderCreateDataDTO> GetCreateData();
        Task Create(LessonReminderCreateDTO dto, int userId);
        Task<bool> DeleteSoftAsync(int userProjectId, int userId);
        Task<ToggleLessonReminderResultDTO> ToggleActiveAsync(int userProjectId, int userId);

        // Filtro de staff_email por proyecto
        Task<List<ProjectStaffReminderConfigItemDTO>> GetAllProjectStaffAsync();
        Task<ToggleProjectStaffReminderResultDTO> ToggleProjectStaffAsync(int projectId);
        Task<List<ActiveProjectStaffEmailDTO>> GetActiveStaffEmailsAsync();

        // Jefaturas (lesson_jefe_reminder) — recordatorio del 4.º día
        Task<List<JefeReminderConfigItemDTO>> GetAllJefesAsync();
        Task<ToggleJefeReminderResultDTO> ToggleJefeAsync(int workerId);
        Task<List<JefeReviewStatusDTO>> GetActiveJefesReviewStatusAsync();
    }
}
