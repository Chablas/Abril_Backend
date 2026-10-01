using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces
{
    public interface IMilestoneScheduleHistoryRepository
    {
        Task<List<MilestoneScheduleHistoryDTO>> GetAllByProjectIdFactory(int projectId);
        Task<ScheduleChangeResult> Create(MilestoneScheduleHistoryCreateDTO dto, int userId);
        /// <summary>Los residentes que no subieron ninguna versión en ese mes (hora de Perú), con sus obras.</summary>
        Task<List<UserWithoutMilestoneDTO>> GetUsersWithoutScheduleHistoryAsync(int anio, int mes);
        Task DeleteAsync(int milestoneScheduleHistoryId, int userId);
    }
}
