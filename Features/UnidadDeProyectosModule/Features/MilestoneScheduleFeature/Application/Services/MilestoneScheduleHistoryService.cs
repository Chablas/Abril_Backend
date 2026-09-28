using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Application.Services
{
    public class MilestoneScheduleHistoryService : IMilestoneScheduleHistoryService
    {
        private readonly IMilestoneScheduleHistoryRepository _repository;
        private readonly ICronogramaPermisosRepository _permisosRepository;

        public MilestoneScheduleHistoryService(
            IMilestoneScheduleHistoryRepository repository,
            ICronogramaPermisosRepository permisosRepository)
        {
            _repository = repository;
            _permisosRepository = permisosRepository;
        }

        public Task<List<MilestoneScheduleHistoryDTO>> GetAllByProjectId(int projectId)
            => _repository.GetAllByProjectIdFactory(projectId);

        /// <summary>Subir una versión nueva (incluido "Guardar sin cambios") es solo del residente
        /// del proyecto: rol RESIDENTE y ser el residente de Emails SSOMA. El featureKey
        /// "mejora-continua.milestone-schedule.editar" es por rol, no por proyecto, así que sin
        /// este chequeo cualquier RESIDENTE subiría el cronograma de cualquier obra. Nadie está
        /// exento: los roles que administran el cronograma no suben versiones.</summary>
        public async Task<ScheduleChangeResult> Create(MilestoneScheduleHistoryCreateDTO dto, int userId, bool esResidente)
        {
            var esResidenteDelProyecto = esResidente
                && await _permisosRepository.EsResidenteDelProyectoAsync(userId, dto.ProjectId);
            if (!esResidenteDelProyecto)
                throw new AbrilException("Solo el residente del proyecto puede subir una nueva versión del cronograma.", 403);

            return await _repository.Create(dto, userId);
        }

        public Task DeleteAsync(int milestoneScheduleHistoryId, int userId)
            => _repository.DeleteAsync(milestoneScheduleHistoryId, userId);
    }
}
