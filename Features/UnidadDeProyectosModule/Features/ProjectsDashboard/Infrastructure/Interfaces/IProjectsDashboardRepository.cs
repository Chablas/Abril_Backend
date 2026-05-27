using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Interfaces
{
    public interface IProjectsDashboardRepository
    {
        Task<(List<ProyectoSimpleDto> Projects, List<string> Estados, List<ResponsableSimpleDto> Responsables)> GetFiltersDataFactory();
        Task<List<ProyectoDetalleDto>> GetDashboardDataAsync(int? proyectoId, string? estado, int? responsableId, DateOnly today);
        Task<List<ResponsableRankingDto>> GetRankingResponsablesAsync(int? proyectoId, string? estado, int? responsableId, DateOnly today);
        Task<List<HeatmapResponsableDto>> GetHeatmapCargaAsync(int? proyectoId, string? estado, int? responsableId, DateOnly fechaDesde, DateOnly fechaHasta);
        Task<ProyectoDetailDashboardDto> GetProyectoDetailAsync(int proyectoId, DateOnly today);
    }
}
