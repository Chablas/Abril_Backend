using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

public interface IResiduoEoRsRepository
{
    Task<List<ResiduoEoRsDto>> ListarAsync(bool? activo, string? tipoOperador);
    Task<SsResiduoEoRs?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoEoRsUpsertDto dto);
    Task<bool> ActualizarAsync(int id, ResiduoEoRsUpsertDto dto);
    Task<bool> DesactivarAsync(int id);

    Task<List<ResiduoEoRsDocumentoDto>> ListarDocumentosAsync(int eoRsId);
    Task<int> CrearDocumentoAsync(int eoRsId, ResiduoEoRsDocumentoUpsertDto dto, string? archivoUrl);
    Task<bool> ActualizarDocumentoAsync(int documentoId, ResiduoEoRsDocumentoUpsertDto dto);
    Task<bool> SetArchivoDocumentoAsync(int documentoId, string archivoUrl);
    Task<bool> EliminarDocumentoAsync(int documentoId);
}
