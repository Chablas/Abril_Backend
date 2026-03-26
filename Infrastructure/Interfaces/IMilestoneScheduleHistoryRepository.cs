using Abril_Backend.Application.DTOs;
using Abril_Backend.Infrastructure.Models;

namespace Abril_Backend.Infrastructure.Interfaces
{
    public interface IMilestoneScheduleHistoryRepository
    {
        Task<List<MilestoneScheduleHistoryDTO>> GetAllByProjectIdFactory(int projectId);
        Task<ScheduleChangeResult> Create(MilestoneScheduleHistoryCreateDTO dto, int userId);
        Task<List<UserWithoutMilestoneDTO>> GetUsersWithoutScheduleHistoryThisMonth();
    }
}
