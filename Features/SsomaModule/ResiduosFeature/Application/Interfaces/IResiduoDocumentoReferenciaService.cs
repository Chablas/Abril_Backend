using Microsoft.AspNetCore.Http;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;

public interface IResiduoDocumentoReferenciaService
{
    Task<List<ResiduoDocumentoReferenciaDto>> ListarAsync(string? tipo, bool? activo);
    Task<ResiduoDocumentoReferenciaDto?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoDocumentoReferenciaUpsertDto dto, IFormFile archivo, int userId);
    Task ActualizarAsync(int id, ResiduoDocumentoReferenciaUpsertDto dto);
    Task<string> SubirArchivoAsync(int id, IFormFile archivo);
    Task DesactivarAsync(int id);
}
