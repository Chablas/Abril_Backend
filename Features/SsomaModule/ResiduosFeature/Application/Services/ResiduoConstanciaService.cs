using Microsoft.AspNetCore.Http;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Services;

public class ResiduoConstanciaService : IResiduoConstanciaService
{
    private readonly IResiduoConstanciaRepository _repo;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;

    public ResiduoConstanciaService(IResiduoConstanciaRepository repo, IFileStorageService fileStorageService, IStorageContainerResolver containerResolver)
    {
        _repo = repo;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
    }

    public Task<List<ResiduoConstanciaDto>> ListarAsync(int? projectId, int? periodoAnio, int? periodoMes) => _repo.ListarAsync(projectId, periodoAnio, periodoMes);

    public async Task<ResiduoConstanciaDto?> ObtenerAsync(int id)
    {
        var entidad = await _repo.ObtenerAsync(id);
        if (entidad == null) return null;
        return new ResiduoConstanciaDto
        {
            Id = entidad.Id,
            ProjectId = entidad.ProjectId,
            EoRsId = entidad.EoRsId,
            NombreEoRs = entidad.EoRs?.RazonSocial,
            Contratista = entidad.Contratista,
            Destino = entidad.Destino,
            PeriodoAnio = entidad.PeriodoAnio,
            PeriodoMes = entidad.PeriodoMes,
            NumeroCertificado = entidad.NumeroCertificado,
            ArchivoUrl = entidad.ArchivoUrl,
        };
    }

    private static void Validar(ResiduoConstanciaUpsertDto dto)
    {
        if (dto.PeriodoMes is < 1 or > 12)
            throw new AbrilException("El mes debe estar entre 1 y 12.", 400);
    }

    public async Task<int> CrearAsync(ResiduoConstanciaUpsertDto dto, IFormFile archivo, int userId)
    {
        Validar(dto);
        if (archivo == null || archivo.Length == 0)
            throw new AbrilException("Debe adjuntar el archivo de la constancia.", 400);

        var container = _containerResolver.GetResiduosContainerName();
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";
        await using var stream = archivo.OpenReadStream();
        var uploadedUrls = await _fileStorageService.UploadFilesAsync(
            new[] { (Stream: (Stream)stream, FileName: fileName) }, container);

        return await _repo.CrearAsync(dto, uploadedUrls.First(), userId);
    }

    public async Task ActualizarAsync(int id, ResiduoConstanciaUpsertDto dto)
    {
        Validar(dto);
        var ok = await _repo.ActualizarAsync(id, dto);
        if (!ok) throw new AbrilException("Constancia no encontrada.", 404);
    }

    public async Task EliminarAsync(int id)
    {
        var ok = await _repo.EliminarAsync(id);
        if (!ok) throw new AbrilException("Constancia no encontrada.", 404);
    }
}
