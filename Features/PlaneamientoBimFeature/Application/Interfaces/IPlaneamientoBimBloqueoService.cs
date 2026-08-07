using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces
{
    public interface IPlaneamientoBimBloqueoService
    {
        Task<List<BloqueoDto>> GetPaged(int projectId, bool? soloActivos);
        Task<BloqueoDto> Create(int projectId, BloqueoCreateDto dto, int userId);
        Task<BloqueoDto> Update(int bloqueoId, BloqueoUpdateDto dto);
        Task<BloqueoDto> Cerrar(int bloqueoId);
    }
}
