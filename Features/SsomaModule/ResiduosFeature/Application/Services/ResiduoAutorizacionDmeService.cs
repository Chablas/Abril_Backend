using Microsoft.AspNetCore.Http;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Services;

public class ResiduoAutorizacionDmeService : IResiduoAutorizacionDmeService
{
    private readonly IResiduoAutorizacionDmeRepository _repo;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;

    public ResiduoAutorizacionDmeService(IResiduoAutorizacionDmeRepository repo, IFileStorageService fileStorageService, IStorageContainerResolver containerResolver)
    {
        _repo = repo;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
    }

    public Task<List<ResiduoAutorizacionDmeDto>> ListarAsync(int? projectId, string? estado) => _repo.ListarAsync(projectId, estado);

    public async Task<ResiduoAutorizacionDmeDto?> ObtenerAsync(int id)
    {
        var entidad = await _repo.ObtenerAsync(id);
        if (entidad == null) return null;
        return new ResiduoAutorizacionDmeDto
        {
            Id = entidad.Id,
            ProjectId = entidad.ProjectId,
            Municipalidad = entidad.Municipalidad,
            NumeroResolucion = entidad.NumeroResolucion,
            EscombreraDestinoId = entidad.EscombreraDestinoId,
            NombreEscombreraDestino = entidad.EscombreraDestino?.RazonSocial,
            VigenciaDesde = entidad.VigenciaDesde,
            VigenciaHasta = entidad.VigenciaHasta,
            PlacasAutorizadas = entidad.PlacasAutorizadas,
            Estado = entidad.Estado,
            ArchivoUrl = entidad.ArchivoUrl,
        };
    }

    private static void Validar(ResiduoAutorizacionDmeUpsertDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Municipalidad))
            throw new AbrilException("La municipalidad es obligatoria.", 400);
        if (string.IsNullOrWhiteSpace(dto.NumeroResolucion))
            throw new AbrilException("El número de resolución es obligatorio.", 400);
        if (dto.VigenciaHasta < dto.VigenciaDesde)
            throw new AbrilException("La vigencia hasta no puede ser anterior a la vigencia desde.", 400);
    }

    public async Task<int> CrearAsync(ResiduoAutorizacionDmeUpsertDto dto, IFormFile? archivo)
    {
        Validar(dto);
        var id = await _repo.CrearAsync(dto);
        if (archivo != null && archivo.Length > 0)
            await SubirArchivoAsync(id, archivo);
        return id;
    }

    public async Task ActualizarAsync(int id, ResiduoAutorizacionDmeUpsertDto dto)
    {
        Validar(dto);
        var ok = await _repo.ActualizarAsync(id, dto);
        if (!ok) throw new AbrilException("Autorización DME no encontrada.", 404);
    }

    public async Task<string> SubirArchivoAsync(int id, IFormFile archivo)
    {
        if (archivo.Length == 0) throw new AbrilException("El archivo está vacío.", 400);

        var container = _containerResolver.GetResiduosContainerName();
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";

        await using var stream = archivo.OpenReadStream();
        var uploadedUrls = await _fileStorageService.UploadFilesAsync(
            new[] { (Stream: (Stream)stream, FileName: fileName) }, container);
        var url = uploadedUrls.First();

        var ok = await _repo.SetArchivoAsync(id, url);
        if (!ok) throw new AbrilException("Autorización DME no encontrada.", 404);
        return url;
    }

    public async Task AnularAsync(int id)
    {
        var ok = await _repo.AnularAsync(id);
        if (!ok) throw new AbrilException("Autorización DME no encontrada.", 404);
    }
}
