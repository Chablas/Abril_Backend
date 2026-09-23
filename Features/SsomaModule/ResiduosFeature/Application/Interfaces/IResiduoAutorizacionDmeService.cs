using Microsoft.AspNetCore.Http;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;

public interface IResiduoAutorizacionDmeService
{
    Task<List<ResiduoAutorizacionDmeDto>> ListarAsync(int? projectId, string? estado);
    Task<ResiduoAutorizacionDmeDto?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoAutorizacionDmeUpsertDto dto, IFormFile? archivo);
    Task ActualizarAsync(int id, ResiduoAutorizacionDmeUpsertDto dto);
    Task<string> SubirArchivoAsync(int id, IFormFile archivo);
    Task AnularAsync(int id);
}
