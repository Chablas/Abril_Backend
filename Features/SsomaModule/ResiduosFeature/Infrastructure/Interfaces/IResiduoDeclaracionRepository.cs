using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

public interface IResiduoDeclaracionRepository
{
    Task<List<ResiduoDeclaracionDto>> ListarAsync(int? contributorId, int? periodoAnio);
    Task<SsResiduoDeclaracion?> ObtenerAsync(int id);
    Task<SsResiduoDeclaracion?> ObtenerPorContributorPeriodoAsync(int contributorId, int periodoAnio);
    Task<int> CrearAsync(ResiduoDeclaracionUpsertDto dto);
    Task<bool> MarcarPresentadaAsync(int id, ResiduoDeclaracionMarcarPresentadaDto dto);
    Task<bool> VolverABorradorAsync(int id);
    Task<bool> EliminarAsync(int id);

    Task<int> UpsertDetalleAsync(int declaracionId, ResiduoDeclaracionDetalleUpsertDto dto);
    Task<bool> EliminarDetalleAsync(int detalleId);

    Task<int> CrearEoRsIntervinienteAsync(int declaracionId, ResiduoDeclaracionEoRsUpsertDto dto);
    Task<bool> ActualizarEoRsIntervinienteAsync(int itemId, ResiduoDeclaracionEoRsUpsertDto dto);
    Task<bool> EliminarEoRsIntervinienteAsync(int itemId);
}
