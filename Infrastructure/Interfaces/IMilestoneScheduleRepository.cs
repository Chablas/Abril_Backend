using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IMilestoneScheduleRepository
    {
        Task<List<MilestoneScheduleDTO>> GetAllByMilestoneScheduleHistoryIdFactory(int milestoneScheduleHistoryId);
        Task<List<ScheduleChangeInfoDTO>> GetSchedulesWithChangesThisMonthAsync();
    }
}
