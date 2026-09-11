using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Interfaces
{
    public interface IVisibilidadAreaService
    {
        Task<VisibilidadInicialDto> GetInitialDataAsync(int ambitoId);
        Task<List<GaAreaNodeDto>> GetAreaTreeAsync();
        Task<List<VisibilidadAsignacionDto>> GetWorkerAsignacionesAsync(int ambitoId, int workerId);
        Task UpdateWorkerAsignacionesAsync(int ambitoId, int workerId, List<VisibilidadAsignacionDto> asignaciones);
    }
}
