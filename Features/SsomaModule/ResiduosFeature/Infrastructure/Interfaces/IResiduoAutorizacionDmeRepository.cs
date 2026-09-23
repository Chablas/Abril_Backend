using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

public interface IResiduoAutorizacionDmeRepository
{
    Task<List<ResiduoAutorizacionDmeDto>> ListarAsync(int? projectId, string? estado);
    Task<SsResiduoAutorizacionDme?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoAutorizacionDmeUpsertDto dto);
    Task<bool> ActualizarAsync(int id, ResiduoAutorizacionDmeUpsertDto dto);
    Task<bool> SetArchivoAsync(int id, string archivoUrl);
    Task<bool> AnularAsync(int id);
}
