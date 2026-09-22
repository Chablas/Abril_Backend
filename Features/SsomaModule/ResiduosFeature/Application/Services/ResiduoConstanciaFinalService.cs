using Microsoft.AspNetCore.Http;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Services;

public class ResiduoConstanciaFinalService : IResiduoConstanciaFinalService
{
    private readonly IResiduoConstanciaFinalRepository _repo;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;

    public ResiduoConstanciaFinalService(IResiduoConstanciaFinalRepository repo, IFileStorageService fileStorageService, IStorageContainerResolver containerResolver)
    {
        _repo = repo;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
    }

    public Task<List<ResiduoConstanciaFinalDto>> ListarAsync(int? projectId) => _repo.ListarAsync(projectId);

    public async Task<ResiduoConstanciaFinalDto?> ObtenerAsync(int id)
    {
        var entidad = await _repo.ObtenerAsync(id);
        if (entidad == null) return null;
        return new ResiduoConstanciaFinalDto
        {
            Id = entidad.Id,
            ProjectId = entidad.ProjectId,
            FechaEmision = entidad.FechaEmision,
            ArchivoUrl = entidad.ArchivoUrl,
            Observaciones = entidad.Observaciones,
        };
    }

    public async Task<int> CrearAsync(ResiduoConstanciaFinalUpsertDto dto, IFormFile archivo, int userId)
    {
        if (archivo == null || archivo.Length == 0)
            throw new AbrilException("Debe adjuntar el archivo de la constancia final.", 400);

        var container = _containerResolver.GetResiduosContainerName();
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";
        await using var stream = archivo.OpenReadStream();
        var uploadedUrls = await _fileStorageService.UploadFilesAsync(
            new[] { (Stream: (Stream)stream, FileName: fileName) }, container);

        return await _repo.CrearAsync(dto, uploadedUrls.First(), userId);
    }

    public async Task ActualizarAsync(int id, ResiduoConstanciaFinalUpsertDto dto)
    {
        var ok = await _repo.ActualizarAsync(id, dto);
        if (!ok) throw new AbrilException("Constancia final no encontrada.", 404);
    }

    public async Task EliminarAsync(int id)
    {
        var ok = await _repo.EliminarAsync(id);
        if (!ok) throw new AbrilException("Constancia final no encontrada.", 404);
    }
}
