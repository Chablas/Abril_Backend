using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

public interface IResiduoDocumentoReferenciaRepository
{
    Task<List<ResiduoDocumentoReferenciaDto>> ListarAsync(string? tipo, bool? activo);
    Task<SsResiduoDocumentoReferencia?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoDocumentoReferenciaUpsertDto dto, string archivoUrl, int userId);
    Task<bool> ActualizarAsync(int id, ResiduoDocumentoReferenciaUpsertDto dto);
    Task<bool> SetArchivoAsync(int id, string archivoUrl);
    Task<bool> DesactivarAsync(int id);
}
