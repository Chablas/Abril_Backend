using Microsoft.AspNetCore.Http;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Services;

public class ResiduoDocumentoReferenciaService : IResiduoDocumentoReferenciaService
{
    private readonly IResiduoDocumentoReferenciaRepository _repo;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;

    public ResiduoDocumentoReferenciaService(IResiduoDocumentoReferenciaRepository repo, IFileStorageService fileStorageService, IStorageContainerResolver containerResolver)
    {
        _repo = repo;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
    }

    public Task<List<ResiduoDocumentoReferenciaDto>> ListarAsync(string? tipo, bool? activo) => _repo.ListarAsync(tipo, activo);

    public async Task<ResiduoDocumentoReferenciaDto?> ObtenerAsync(int id)
    {
        var entidad = await _repo.ObtenerAsync(id);
        if (entidad == null) return null;
        return new ResiduoDocumentoReferenciaDto
        {
            Id = entidad.Id,
            Tipo = entidad.Tipo,
            Nombre = entidad.Nombre,
            Descripcion = entidad.Descripcion,
            ArchivoUrl = entidad.ArchivoUrl,
            Version = entidad.Version,
            Activo = entidad.Activo,
        };
    }

    private static void Validar(ResiduoDocumentoReferenciaUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Tipo))
            throw new AbrilException("El tipo de documento es obligatorio.", 400);
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new AbrilException("El nombre es obligatorio.", 400);
    }

    public async Task<int> CrearAsync(ResiduoDocumentoReferenciaUpsertDto dto, IFormFile archivo, int userId)
    {
        Validar(dto);
        if (archivo == null || archivo.Length == 0)
            throw new AbrilException("Debe adjuntar el archivo.", 400);

        var url = await SubirArchivoInternoAsync(archivo);
        return await _repo.CrearAsync(dto, url, userId);
    }

    public async Task ActualizarAsync(int id, ResiduoDocumentoReferenciaUpsertDto dto)
    {
        Validar(dto);
        var ok = await _repo.ActualizarAsync(id, dto);
        if (!ok) throw new AbrilException("Documento no encontrado.", 404);
    }

    public async Task<string> SubirArchivoAsync(int id, IFormFile archivo)
    {
        var url = await SubirArchivoInternoAsync(archivo);
        var ok = await _repo.SetArchivoAsync(id, url);
        if (!ok) throw new AbrilException("Documento no encontrado.", 404);
        return url;
    }

    private async Task<string> SubirArchivoInternoAsync(IFormFile archivo)
    {
        if (archivo.Length == 0) throw new AbrilException("El archivo está vacío.", 400);

        var container = _containerResolver.GetResiduosContainerName();
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";

        await using var stream = archivo.OpenReadStream();
        var uploadedUrls = await _fileStorageService.UploadFilesAsync(
            new[] { (Stream: (Stream)stream, FileName: fileName) }, container);

        return uploadedUrls.First();
    }

    public async Task DesactivarAsync(int id)
    {
        var ok = await _repo.DesactivarAsync(id);
        if (!ok) throw new AbrilException("Documento no encontrado.", 404);
    }
}
