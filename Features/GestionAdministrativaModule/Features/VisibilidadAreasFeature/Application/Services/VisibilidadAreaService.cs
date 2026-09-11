using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Dtos;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Interfaces;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Infrastructure.Interfaces;

namespace Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Services
{
    public class VisibilidadAreaService : IVisibilidadAreaService
    {
        private readonly IVisibilidadAreaRepository _repo;

        public VisibilidadAreaService(IVisibilidadAreaRepository repo)
        {
            _repo = repo;
        }

        public Task<VisibilidadInicialDto> GetInitialDataAsync(int ambitoId)
            => _repo.GetInitialDataAsync(ambitoId);

        public Task<List<GaAreaNodeDto>> GetAreaTreeAsync()
            => _repo.GetAreaTreeAsync();

        public Task<List<VisibilidadAsignacionDto>> GetWorkerAsignacionesAsync(int ambitoId, int workerId)
            => _repo.GetWorkerAsignacionesAsync(ambitoId, workerId);

        public Task UpdateWorkerAsignacionesAsync(
            int ambitoId, int workerId, List<VisibilidadAsignacionDto> asignaciones)
            => _repo.UpdateWorkerAsignacionesAsync(ambitoId, workerId, asignaciones);
    }
}
