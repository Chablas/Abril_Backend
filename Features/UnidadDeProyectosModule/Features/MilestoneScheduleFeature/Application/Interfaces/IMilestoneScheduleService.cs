using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces
{
    public interface IMilestoneScheduleService
    {
        Task<List<MilestoneScheduleDTO>> GetAllByMilestoneScheduleHistoryId(int milestoneScheduleHistoryId);
        Task<List<MilestoneScheduleFakeDataDTO>> BuildFakeSchedule();
        Task CulminarAsync(int milestoneScheduleId, DateOnly? fechaRealFin, int userId, bool esAdminResidentes);
        Task MarcarCriticoAsync(int milestoneScheduleId, bool esHitoCritico, int userId, bool esAdminResidentes);
    }
}
