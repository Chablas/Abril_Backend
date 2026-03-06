using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IMilestoneScheduleHistoryRepository
    {
        Task<List<MilestoneScheduleHistoryDTO>> GetAllByScheduleIdFactory(int scheduleId);
        Task<ScheduleChangeResult> Create(MilestoneScheduleHistoryCreateDTO dto, int userId);
        Task<List<UserWithoutMilestoneDTO>> GetUsersWithoutScheduleHistoryThisMonth();
    }
}
