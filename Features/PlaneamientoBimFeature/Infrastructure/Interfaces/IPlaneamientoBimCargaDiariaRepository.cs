using Abril_Backend.Features.PlaneamientoBimFeature.Application.Dtos;

namespace Abril_Backend.Features.PlaneamientoBimFeature.Infrastructure.Interfaces
{
    public interface IPlaneamientoBimCargaDiariaRepository
    {
        Task<CargaDiariaDto?> GetCargaDiaria(int projectId, DateOnly fecha, string categoria);
        Task<List<NivelRangoSectorDto>> GetRangosSectorPorNivel(int projectId);
        Task GuardarCargaDiaria(int projectId, DateOnly fecha, CargaDiariaUpdateDto dto, int userId);
        Task<List<EvidenciaFotoDto>> AgregarEvidencias(int projectId, DateOnly fecha, List<string> urls, int userId, string categoria);
    }
}
