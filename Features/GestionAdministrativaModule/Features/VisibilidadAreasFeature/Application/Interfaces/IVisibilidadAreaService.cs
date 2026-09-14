using Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Interfaces
{
    public interface IVisibilidadAreaService
    {
        Task<VisibilidadInicialDto> GetInitialDataAsync(int ambitoId);
        Task<VisibilidadWorkerDetalleDto> GetWorkerDetalleAsync(int ambitoId, int workerId);
        Task UpdateWorkerAsignacionesAsync(int ambitoId, int workerId, List<VisibilidadAsignacionDto> asignaciones);
    }
}
