using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

public interface IResiduoConstanciaRepository
{
    Task<List<ResiduoConstanciaDto>> ListarAsync(int? projectId, int? periodoAnio, int? periodoMes);
    Task<SsResiduoConstancia?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoConstanciaUpsertDto dto, string archivoUrl, int userId);
    Task<bool> ActualizarAsync(int id, ResiduoConstanciaUpsertDto dto);
    Task<bool> EliminarAsync(int id);
}
