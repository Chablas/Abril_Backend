namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IMonthlyLessonReminderService
    {
        Task SendLessonsLearnedMonthlyRemindersAsync(DateTime executionDate);
        Task NotifySupervisorsAboutPendingLessonsAsync(DateTime executionDate);
        Task SendMilestoneScheduleMonthlyReminderAsync(DateTime executionDate);
        Task SendMilestoneScheduleHistoryMonthlyRemindersAsync(DateTime executionDate);
    }
}
