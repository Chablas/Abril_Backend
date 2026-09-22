using Microsoft.AspNetCore.Http;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;

public interface IResiduoConstanciaFinalService
{
    Task<List<ResiduoConstanciaFinalDto>> ListarAsync(int? projectId);
    Task<ResiduoConstanciaFinalDto?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoConstanciaFinalUpsertDto dto, IFormFile archivo, int userId);
    Task ActualizarAsync(int id, ResiduoConstanciaFinalUpsertDto dto);
    Task EliminarAsync(int id);
}
