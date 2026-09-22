using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

public interface IResiduoConstanciaFinalRepository
{
    Task<List<ResiduoConstanciaFinalDto>> ListarAsync(int? projectId);
    Task<SsResiduoConstanciaFinal?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoConstanciaFinalUpsertDto dto, string archivoUrl, int userId);
    Task<bool> ActualizarAsync(int id, ResiduoConstanciaFinalUpsertDto dto);
    Task<bool> EliminarAsync(int id);
}
