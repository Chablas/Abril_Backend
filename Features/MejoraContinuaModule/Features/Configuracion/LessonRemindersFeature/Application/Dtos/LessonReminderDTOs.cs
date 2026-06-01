namespace Abril_Backend.Features.MejoraContinuaModule.Features.Configuracion.LessonRemindersFeature.Application.Dtos
{
    public class LessonReminderDTO
    {
        public int UserProjectId { get; set; }
        public int UserId { get; set; }
        public string? UserFullName { get; set; }
        public int ProjectId { get; set; }
        public string? ProjectDescription { get; set; }
        public DateTime CreatedDateTime { get; set; }
        public int CreatedUserId { get; set; }
        public DateTime? UpdatedDateTime { get; set; }
        public int? UpdatedUserId { get; set; }
        public bool Active { get; set; }
    }

    public class LessonReminderCreateDTO
    {
        public int UserId { get; set; }
        public int ProjectId { get; set; }
        public bool Active { get; set; }
    }

    public class ToggleLessonReminderResultDTO
    {
        public int UserProjectId { get; set; }
        public bool Active { get; set; }
    }

    public class LessonReminderCreateDataDTO
    {
        public List<LessonReminderUserDTO> Users { get; set; } = new();
        public List<LessonReminderProjectDTO> Projects { get; set; } = new();
    }

    public class LessonReminderUserDTO
    {
        public int UserId { get; set; }
        public string? FullName { get; set; }
    }

    public class LessonReminderProjectDTO
    {
        public int ProjectId { get; set; }
        public string? ProjectDescription { get; set; }
    }

    /// <summary>
    /// Una fila por proyecto que tiene staff_email registrado.
    /// Refleja el toggle de project_staff_reminder.active.
    /// </summary>
    public class ProjectStaffReminderConfigItemDTO
    {
        public int? ProjectStaffReminderId { get; set; } // null si todavía no se ha togglado
        public int ProjectId { get; set; }
        public string? ProjectDescription { get; set; }
        public string? StaffEmail { get; set; }
        public bool Active { get; set; }
    }

    public class ToggleProjectStaffReminderResultDTO
    {
        public int ProjectStaffReminderId { get; set; }
        public bool Active { get; set; }
    }

    /// <summary>Proyecto + email staff activo. Usado por el cron de recordatorios.</summary>
    public class ActiveProjectStaffEmailDTO
    {
        public int ProjectId { get; set; }
        public string ProjectDescription { get; set; } = string.Empty;
        public string StaffEmail { get; set; } = string.Empty;
    }
}
