using Microsoft.AspNetCore.Http;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Services;

public class ResiduoEoRsService : IResiduoEoRsService
{
    private readonly IResiduoEoRsRepository _repo;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;

    public ResiduoEoRsService(IResiduoEoRsRepository repo, IFileStorageService fileStorageService, IStorageContainerResolver containerResolver)
    {
        _repo = repo;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
    }

    public Task<List<ResiduoEoRsDto>> ListarAsync(bool? activo, string? tipoOperador) => _repo.ListarAsync(activo, tipoOperador);

    public async Task<ResiduoEoRsDto?> ObtenerAsync(int id)
    {
        var entidad = await _repo.ObtenerAsync(id);
        if (entidad == null) return null;
        return new ResiduoEoRsDto
        {
            Id = entidad.Id,
            Ruc = entidad.Ruc,
            RazonSocial = entidad.RazonSocial,
            TipoOperador = entidad.TipoOperador,
            NumeroRegistroMinam = entidad.NumeroRegistroMinam,
            VigenciaRegistro = entidad.VigenciaRegistro,
            Direccion = entidad.Direccion,
            AmbitoGestion = entidad.AmbitoGestion,
            Activo = entidad.Activo,
        };
    }

    public Task<int> CrearAsync(ResiduoEoRsUpsertDto dto)
    {
        Validar(dto);
        return _repo.CrearAsync(dto);
    }

    public async Task ActualizarAsync(int id, ResiduoEoRsUpsertDto dto)
    {
        Validar(dto);
        var ok = await _repo.ActualizarAsync(id, dto);
        if (!ok) throw new AbrilException("EO-RS no encontrado.", 404);
    }

    private static void Validar(ResiduoEoRsUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ruc) || dto.Ruc.Length != 11)
            throw new AbrilException("El RUC debe tener 11 dígitos.", 400);
        if (string.IsNullOrWhiteSpace(dto.RazonSocial))
            throw new AbrilException("La razón social es obligatoria.", 400);
        if (string.IsNullOrWhiteSpace(dto.TipoOperador))
            throw new AbrilException("El tipo de operador es obligatorio.", 400);
    }

    public async Task DesactivarAsync(int id)
    {
        var ok = await _repo.DesactivarAsync(id);
        if (!ok) throw new AbrilException("EO-RS no encontrado.", 404);
    }

    public Task<List<ResiduoEoRsDocumentoDto>> ListarDocumentosAsync(int eoRsId) => _repo.ListarDocumentosAsync(eoRsId);

    public async Task<int> CrearDocumentoAsync(int eoRsId, ResiduoEoRsDocumentoUpsertDto dto, IFormFile? archivo)
    {
        if (string.IsNullOrWhiteSpace(dto.TipoDocumento))
            throw new AbrilException("El tipo de documento es obligatorio.", 400);

        string? archivoUrl = null;
        if (archivo != null && archivo.Length > 0)
            archivoUrl = await SubirArchivoAsync(archivo);

        return await _repo.CrearDocumentoAsync(eoRsId, dto, archivoUrl);
    }

    public async Task ActualizarDocumentoAsync(int documentoId, ResiduoEoRsDocumentoUpsertDto dto)
    {
        var ok = await _repo.ActualizarDocumentoAsync(documentoId, dto);
        if (!ok) throw new AbrilException("Documento no encontrado.", 404);
    }

    public async Task<string> SubirArchivoDocumentoAsync(int documentoId, IFormFile archivo)
    {
        var url = await SubirArchivoAsync(archivo);
        var ok = await _repo.SetArchivoDocumentoAsync(documentoId, url);
        if (!ok) throw new AbrilException("Documento no encontrado.", 404);
        return url;
    }

    private async Task<string> SubirArchivoAsync(IFormFile archivo)
    {
        if (archivo.Length == 0) throw new AbrilException("El archivo está vacío.", 400);

        var container = _containerResolver.GetResiduosContainerName();
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";

        await using var stream = archivo.OpenReadStream();
        var uploadedUrls = await _fileStorageService.UploadFilesAsync(
            new[] { (Stream: (Stream)stream, FileName: fileName) }, container);

        return uploadedUrls.First();
    }

    public async Task EliminarDocumentoAsync(int documentoId)
    {
        var ok = await _repo.EliminarDocumentoAsync(documentoId);
        if (!ok) throw new AbrilException("Documento no encontrado.", 404);
    }
}
