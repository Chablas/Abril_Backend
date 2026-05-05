using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Repositories;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.LessonsLearnedDashboard.Application.Services
{
    public class LessonsLearnedDashboardService : ILessonsLearnedDashboardService
    {
        private readonly ILessonsLearnedDashboardRepository _dashboardRepository;
        private readonly AreaRepository _areaRepository;
        private readonly ProjectRepository _projectRepository;
        private readonly PhaseRepository _phaseRepository;
        private readonly StageRepository _stageRepository;
        private readonly LayerRepository _layerRepository;
        private readonly SubStageRepository _subStageRepository;
        private readonly SubSpecialtyRepository _subSpecialtyRepository;
        private readonly IUserRepository _userRepository;

        public LessonsLearnedDashboardService(
            ILessonsLearnedDashboardRepository dashboardRepository,
            AreaRepository areaRepository,
            ProjectRepository projectRepository,
            PhaseRepository phaseRepository,
            StageRepository stageRepository,
            LayerRepository layerRepository,
            SubStageRepository subStageRepository,
            SubSpecialtyRepository subSpecialtyRepository,
            IUserRepository userRepository
        )
        {
            _dashboardRepository = dashboardRepository;
            _areaRepository = areaRepository;
            _projectRepository = projectRepository;
            _phaseRepository = phaseRepository;
            _stageRepository = stageRepository;
            _layerRepository = layerRepository;
            _subStageRepository = subStageRepository;
            _subSpecialtyRepository = subSpecialtyRepository;
            _userRepository = userRepository;
        }

        public async Task<LessonsLearnedDashboardFiltersResponseDto> GetFilters()
        {
            var areasTask = _areaRepository.GetAllFactory();
            var projectsTask = _projectRepository.GetAllFactory();
            var periodsTask = _dashboardRepository.GetAllPeriodsFactory();
            var phasesTask = _phaseRepository.GetAllFactory();
            var stagesTask = _stageRepository.GetAllFactory();
            var layersTask = _layerRepository.GetAllFactory();
            var subStagesTask = _subStageRepository.GetAllFactory();
            var subSpecialtiesTask = _subSpecialtyRepository.GetAllFactory();
            var usersTask = _userRepository.GetAllUsersFactory();

            await Task.WhenAll(areasTask, projectsTask, periodsTask, phasesTask, stagesTask, layersTask, subStagesTask, subSpecialtiesTask, usersTask);

            return new LessonsLearnedDashboardFiltersResponseDto
            {
                Areas = areasTask.Result,
                Projects = projectsTask.Result,
                Periods = periodsTask.Result,
                Phases = phasesTask.Result,
                Stages = stagesTask.Result,
                Layers = layersTask.Result,
                SubStages = subStagesTask.Result,
                SubSpecialties = subSpecialtiesTask.Result,
                Users = usersTask.Result
            };
        }
    }
}
