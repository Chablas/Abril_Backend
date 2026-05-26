using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Interfaces
{
    public interface IProjectsDashboardRepository
    {
        Task<(List<string> Estados, List<ResponsableArqComSimpleDto> ResponsablesArqCom)> GetFiltersDataFactory();
        Task<List<ProyectoDetalleDto>> GetDashboardDataAsync(int? proyectoId, string? estado, int? responsableArqComId, DateOnly today);
        Task<List<ResponsableRankingDto>> GetRankingResponsablesAsync(int? proyectoId, string? estado, int? responsableArqComId, DateOnly today);
        Task<List<HeatmapCargaItemDto>> GetHeatmapCargaAsync(int? proyectoId, string? estado, int? responsableArqComId, DateOnly fechaDesde, DateOnly fechaHasta);
        Task<ProyectoDetailDashboardDto> GetProyectoDetailAsync(int proyectoId, DateOnly today);
    }
}
