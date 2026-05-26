using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Interfaces;
using Abril_Backend.Infrastructure.Repositories;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Services
{
    public class ProjectsDashboardService : IProjectsDashboardService
    {
        private readonly IProjectsDashboardRepository _dashboardRepository;
        private readonly ProjectRepository _projectRepository;

        public ProjectsDashboardService(
            IProjectsDashboardRepository dashboardRepository,
            ProjectRepository projectRepository)
        {
            _dashboardRepository = dashboardRepository;
            _projectRepository = projectRepository;
        }

        public async Task<ProjectsDashboardFiltersResponseDto> GetFilters()
        {
            var projectsTask = _projectRepository.GetAllFactory();
            var filtersTask = _dashboardRepository.GetFiltersDataFactory();

            await Task.WhenAll(projectsTask, filtersTask);
            var (estados, responsables) = filtersTask.Result;

            return new ProjectsDashboardFiltersResponseDto
            {
                Projects = projectsTask.Result,
                Estados = estados,
                ResponsablesArqCom = responsables
            };
        }

        public async Task<ProjectsDashboardResponseDto> GetDashboard(int? proyectoId, string? estado, int? responsableArqComId, DateOnly? fechaDesde, DateOnly? fechaHasta)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var heatDesde = fechaDesde ?? today;
            var heatHasta = fechaHasta ?? today.AddDays(12 * 7);

            var proyectosTask = _dashboardRepository.GetDashboardDataAsync(proyectoId, estado, responsableArqComId, today);
            var rankingTask = _dashboardRepository.GetRankingResponsablesAsync(proyectoId, estado, responsableArqComId, today);
            var heatmapTask = _dashboardRepository.GetHeatmapCargaAsync(proyectoId, estado, responsableArqComId, heatDesde, heatHasta);

            await Task.WhenAll(proyectosTask, rankingTask, heatmapTask);

            var proyectos = proyectosTask.Result;
            var ranking = rankingTask.Result;
            var heatmap = heatmapTask.Result;

            var total = proyectos.Count;
            var sinActividades = proyectos.Count(p => p.TotalActividades == 0);
            var conRetraso = proyectos.Count(p => p.EstaConRetraso);
            var alDia = proyectos.Count(p => p.TotalActividades > 0 && !p.EstaConRetraso);
            var avancePromedio = total > 0 ? Math.Round(proyectos.Average(p => p.PorcentajeAvance), 1) : 0d;

            var distribucion = proyectos
                .GroupBy(p => p.Estado ?? "(Sin estado)")
                .Select(g => new EstadoDistribucionDto { Estado = g.Key, CantidadProyectos = g.Count() })
                .OrderBy(d => d.Estado)
                .ToList();

            return new ProjectsDashboardResponseDto
            {
                TotalProyectos = total,
                AlDia = alDia,
                ConRetraso = conRetraso,
                SinActividades = sinActividades,
                PorcentajeAvancePromedio = avancePromedio,
                Proyectos = proyectos,
                DistribucionPorEstado = distribucion,
                RankingResponsables = ranking,
                HeatmapCarga = heatmap
            };
        }

        public async Task<ProyectoDetailDashboardDto> GetProyectoDetail(int proyectoId)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return await _dashboardRepository.GetProyectoDetailAsync(proyectoId, today);
        }
    }
}
