using Microsoft.AspNetCore.Http;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;

public interface IResiduoDeclaracionService
{
    Task<List<ResiduoDeclaracionDto>> ListarAsync(int? contributorId, int? periodoAnio);
    Task<ResiduoDeclaracionDto> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoDeclaracionUpsertDto dto);
    Task MarcarPresentadaAsync(int id, ResiduoDeclaracionMarcarPresentadaDto dto);
    Task<string> SubirArchivoConstanciaAsync(int id, IFormFile archivo);
    Task VolverABorradorAsync(int id);
    Task EliminarAsync(int id);

    Task<int> UpsertDetalleAsync(int declaracionId, ResiduoDeclaracionDetalleUpsertDto dto);
    Task EliminarDetalleAsync(int detalleId);

    Task<int> CrearEoRsIntervinienteAsync(int declaracionId, ResiduoDeclaracionEoRsUpsertDto dto);
    Task ActualizarEoRsIntervinienteAsync(int itemId, ResiduoDeclaracionEoRsUpsertDto dto);
    Task EliminarEoRsIntervinienteAsync(int itemId);

    /// <summary>
    /// Genera/recalcula la declaración de un contributor+año agregando automáticamente desde
    /// ss_residuo_viaje los montos mensuales por tipo de residuo y por tipo de manejo, y hace
    /// upsert de las filas de detalle. No sobrescribe declaraciones PRESENTADAS salvo que
    /// `forzar` sea true.
    /// </summary>
    Task<ResiduoDeclaracionDto> RecalcularAsync(ResiduoDeclaracionRecalcularDto dto);
}
