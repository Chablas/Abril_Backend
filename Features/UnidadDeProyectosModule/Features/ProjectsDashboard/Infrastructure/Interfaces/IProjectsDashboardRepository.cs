using Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.ProjectsDashboard.Infrastructure.Interfaces
{
    public interface IProjectsDashboardRepository
    {
        Task<(List<string> Estados, List<ResponsableArqComSimpleDto> ResponsablesArqCom)> GetFiltersDataFactory();
        Task<List<ProyectoDetalleDto>> GetDashboardDataFactory(int? proyectoId, string? estado, int? responsableArqComId);
    }
}
