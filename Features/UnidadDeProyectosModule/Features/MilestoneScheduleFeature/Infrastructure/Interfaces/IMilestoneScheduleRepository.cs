using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces
{
    public interface IMilestoneScheduleRepository
    {
        Task<List<MilestoneScheduleDTO>> GetAllByMilestoneScheduleHistoryIdFactory(int milestoneScheduleHistoryId);
        /// <summary>Las versiones subidas entre <paramref name="desdeUtc"/> (incluido) y <paramref name="hastaUtc"/>, por proyecto y usuario.</summary>
        Task<List<ScheduleChangeInfoDTO>> GetSchedulesWithChangesAsync(DateTime desdeUtc, DateTime hastaUtc);
        Task<int?> GetProjectIdByMilestoneScheduleId(int milestoneScheduleId);
        Task CulminarAsync(int milestoneScheduleId, DateOnly? fechaRealFin, int userId);
        Task MarcarCriticoAsync(int milestoneScheduleId, bool esHitoCritico, int userId);
        Task EditAsync(int milestoneScheduleId, MilestoneScheduleEditDTO dto, int userId);
        Task<MilestoneScheduleDTO> AddHitoAsync(int milestoneScheduleHistoryId, MilestoneScheduleAddDTO dto, int userId);
        Task<List<MilestoneSimpleDTO>> GetFaltantesAsync(int projectId);
    }
}
