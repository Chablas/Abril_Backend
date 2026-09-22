using Microsoft.AspNetCore.Http;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;

public interface IResiduoViajeService
{
    Task<ResiduoViajePagedDto> ListarAsync(ResiduoViajeListFiltroDto filtro);
    Task<ResiduoViajeDto?> ObtenerAsync(long id);
    Task<long> CrearAsync(ResiduoViajeUpsertDto dto, int userId);
    Task ActualizarAsync(long id, ResiduoViajeUpsertDto dto);
    Task DesactivarAsync(long id);
    Task<string> SubirArchivoAsync(long id, IFormFile archivo);
}
