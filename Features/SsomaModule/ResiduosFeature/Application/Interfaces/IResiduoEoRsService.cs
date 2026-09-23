using Microsoft.AspNetCore.Http;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;

public interface IResiduoEoRsService
{
    Task<List<ResiduoEoRsDto>> ListarAsync(bool? activo, string? tipoOperador);
    Task<ResiduoEoRsDto?> ObtenerAsync(int id);
    Task<int> CrearAsync(ResiduoEoRsUpsertDto dto);
    Task ActualizarAsync(int id, ResiduoEoRsUpsertDto dto);
    Task DesactivarAsync(int id);

    Task<List<ResiduoEoRsDocumentoDto>> ListarDocumentosAsync(int eoRsId);
    Task<int> CrearDocumentoAsync(int eoRsId, ResiduoEoRsDocumentoUpsertDto dto, IFormFile? archivo);
    Task ActualizarDocumentoAsync(int documentoId, ResiduoEoRsDocumentoUpsertDto dto);
    Task<string> SubirArchivoDocumentoAsync(int documentoId, IFormFile archivo);
    Task EliminarDocumentoAsync(int documentoId);
}
