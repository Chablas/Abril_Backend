using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Dtos;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Interfaces;
using Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Infrastructure.Interfaces;

namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.CronogramaActividades.Application.Services
{
    public class CronogramaActividadesService : ICronogramaActividadesService
    {
        private readonly ICronogramaActividadesRepository _repository;

        public CronogramaActividadesService(ICronogramaActividadesRepository repository)
        {
            _repository = repository;
        }

        public Task<List<ProyectoSimpleCronogramaDto>> GetProyectosAsync()
            => _repository.GetProyectosAsync();

        public Task<List<ActividadDto>> GetActividadesAsync(int proyectoId)
            => _repository.GetActividadesAsync(proyectoId);

        public Task<ActividadDto> CrearActividadAsync(int proyectoId, CrearActividadRequest request, int userId)
            => _repository.CrearActividadAsync(proyectoId, request, userId);

        public Task<ActividadDto> EditarActividadAsync(int projectActivityId, EditarActividadRequest request, int userId)
            => _repository.EditarActividadAsync(projectActivityId, request, userId);

        public Task<CulminarActividadDto> CulminarActividadAsync(int projectActivityId, int userId)
            => _repository.CulminarActividadAsync(projectActivityId, userId);

        public Task EliminarActividadAsync(int projectActivityId, int userId)
            => _repository.EliminarActividadAsync(projectActivityId, userId);
    }
}
