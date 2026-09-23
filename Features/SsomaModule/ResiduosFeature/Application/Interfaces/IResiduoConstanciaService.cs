using Microsoft.AspNetCore.Http;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;

public interface IResiduoConstanciaService
{
    Task<List<ResiduoConstanciaDto>> ListarAsync(int? projectId, int? periodoAnio, int? periodoMes);
    Task<ResiduoConstanciaDto?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoConstanciaUpsertDto dto, IFormFile archivo, int userId);
    Task ActualizarAsync(int id, ResiduoConstanciaUpsertDto dto);
    Task EliminarAsync(int id);
}
