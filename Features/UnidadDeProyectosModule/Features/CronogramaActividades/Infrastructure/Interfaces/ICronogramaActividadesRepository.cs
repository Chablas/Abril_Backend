using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Interfaces
{
    public interface ICronogramaActividadesRepository
    {
        Task<List<ProyectoSimpleCronogramaDto>> GetProyectosAsync();
        Task<List<ActividadDto>> GetActividadesAsync(int proyectoId);
        Task<ActividadDto> CrearActividadAsync(int proyectoId, CrearActividadRequest request, int userId);
        Task<ActividadDto> EditarActividadAsync(int projectActivityId, EditarActividadRequest request, int userId);
        Task<CulminarActividadDto> CulminarActividadAsync(int projectActivityId, int userId);
        Task EliminarActividadAsync(int projectActivityId, int userId);
    }
}
