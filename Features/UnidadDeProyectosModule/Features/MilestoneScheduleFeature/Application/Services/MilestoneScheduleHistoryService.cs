using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Services
{
    public class MilestoneScheduleHistoryService : IMilestoneScheduleHistoryService
    {
        private readonly IMilestoneScheduleHistoryRepository _repository;
        private readonly IProjectResidentRepository _projectResidentRepository;

        public MilestoneScheduleHistoryService(
            IMilestoneScheduleHistoryRepository repository,
            IProjectResidentRepository projectResidentRepository)
        {
            _repository = repository;
            _projectResidentRepository = projectResidentRepository;
        }

        public Task<List<MilestoneScheduleHistoryDTO>> GetAllByProjectId(int projectId)
            => _repository.GetAllByProjectIdFactory(projectId);

        /// <summary>Solo el residente asignado al proyecto puede guardar su cronograma — el
        /// featureKey "mejora-continua.milestone-schedule.editar" es por rol, no por proyecto,
        /// así que cualquier RESIDENTE podía editar el cronograma de cualquier obra sin esto.
        /// El rol ADMINISTRADOR DE RESIDENTES está exento: supervisa a todos los residentes,
        /// así que puede editar el cronograma de cualquier proyecto.</summary>
        public async Task<ScheduleChangeResult> Create(MilestoneScheduleHistoryCreateDTO dto, int userId, bool esAdminResidentes)
        {
            if (!esAdminResidentes)
            {
                var esResidenteAsignado = await _projectResidentRepository.IsUserAssignedToProject(userId, dto.ProjectId);
                if (!esResidenteAsignado)
                    throw new AbrilException("No estás asignado como residente de este proyecto.", 403);
            }

            return await _repository.Create(dto, userId);
        }

        public Task DeleteAsync(int milestoneScheduleHistoryId, int userId)
            => _repository.DeleteAsync(milestoneScheduleHistoryId, userId);
    }
}
