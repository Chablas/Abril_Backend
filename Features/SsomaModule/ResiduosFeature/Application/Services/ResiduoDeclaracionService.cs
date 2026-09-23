using Microsoft.AspNetCore.Http;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Services;

public class ResiduoDeclaracionService : IResiduoDeclaracionService
{
    private readonly IResiduoDeclaracionRepository _repo;
    private readonly IResiduoViajeRepository _viajeRepo;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;

    public ResiduoDeclaracionService(
        IResiduoDeclaracionRepository repo,
        IResiduoViajeRepository viajeRepo,
        IFileStorageService fileStorageService,
        IStorageContainerResolver containerResolver)
    {
        _repo = repo;
        _viajeRepo = viajeRepo;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
    }

    public Task<List<ResiduoDeclaracionDto>> ListarAsync(int? contributorId, int? periodoAnio) =>
        _repo.ListarAsync(contributorId, periodoAnio);

    private static ResiduoDeclaracionDto Proyectar(SsResiduoDeclaracion d) => new()
    {
        Id = d.Id,
        ContributorId = d.ContributorId,
        NombreContributor = d.Contributor?.ContributorName,
        PeriodoAnio = d.PeriodoAnio,
        Estado = d.Estado,
        FechaPresentacion = d.FechaPresentacion,
        ArchivoConstanciaUrl = d.ArchivoConstanciaUrl,
        Detalles = d.Detalles.Select(det => new ResiduoDeclaracionDetalleDto
        {
            Id = det.Id,
            ResiduoTipoId = det.ResiduoTipoId,
            NombreResiduoTipo = det.ResiduoTipo?.Nombre,
            CantidadAcumuladaAnterior = det.CantidadAcumuladaAnterior,
            Ene = det.Ene, Feb = det.Feb, Mar = det.Mar, Abr = det.Abr, May = det.May, Jun = det.Jun,
            Jul = det.Jul, Ago = det.Ago, Sep = det.Sep, Oct = det.Oct, Nov = det.Nov, Dic = det.Dic,
            Almacenado = det.Almacenado,
            Tratado = det.Tratado,
            Acondicionado = det.Acondicionado,
            Valorizado = det.Valorizado,
            Comercializado = det.Comercializado,
            DisposicionFinal = det.DisposicionFinal,
        }).ToList(),
        EoRsIntervinientes = d.EoRsIntervinientes.Select(e => new ResiduoDeclaracionEoRsDto
        {
            Id = e.Id,
            EoRsId = e.EoRsId,
            NombreEoRs = e.EoRs?.RazonSocial,
            Etapa = e.Etapa,
            NumeroServiciosAnio = e.NumeroServiciosAnio,
            TotalResiduoTon = e.TotalResiduoTon,
        }).ToList(),
    };

    public async Task<ResiduoDeclaracionDto> ObtenerAsync(int id)
    {
        var entidad = await _repo.ObtenerAsync(id) ?? throw new AbrilException("Declaración no encontrada.", 404);
        return Proyectar(entidad);
    }

    public Task<int> CrearAsync(ResiduoDeclaracionUpsertDto dto) => _repo.CrearAsync(dto);

    /// <summary>
    /// Genera/recalcula el detalle de la declaración de un contributor+año agregando automáticamente
    /// desde ss_residuo_viaje los montos mensuales por tipo de residuo y por tipo de manejo. Crea la
    /// declaración si no existe. No sobrescribe una declaración ya PRESENTADA salvo que se pase
    /// forzarRecalculo=true.
    /// </summary>
    public async Task<ResiduoDeclaracionDto> RecalcularAsync(ResiduoDeclaracionRecalcularDto dto)
    {
        var existente = await _repo.ObtenerPorContributorPeriodoAsync(dto.ContributorId, dto.PeriodoAnio);

        if (existente != null && existente.Estado == "PRESENTADA" && !dto.Forzar)
            throw new AbrilException(
                "La declaración de este periodo ya fue presentada. Use forzarRecalculo=true para recalcularla de todas formas.",
                400);

        var declaracionId = existente?.Id ?? await _repo.CrearAsync(new ResiduoDeclaracionUpsertDto
        {
            ContributorId = dto.ContributorId,
            PeriodoAnio = dto.PeriodoAnio,
        });

        var agregados = await _viajeRepo.ObtenerAgregadoMensualAsync(dto.ContributorId, dto.PeriodoAnio);

        foreach (var fila in agregados)
        {
            await _repo.UpsertDetalleAsync(declaracionId, new ResiduoDeclaracionDetalleUpsertDto
            {
                ResiduoTipoId = fila.ResiduoTipoId,
                CantidadAcumuladaAnterior = 0,
                Ene = fila.Meses[0], Feb = fila.Meses[1], Mar = fila.Meses[2], Abr = fila.Meses[3],
                May = fila.Meses[4], Jun = fila.Meses[5], Jul = fila.Meses[6], Ago = fila.Meses[7],
                Sep = fila.Meses[8], Oct = fila.Meses[9], Nov = fila.Meses[10], Dic = fila.Meses[11],
                Almacenado = fila.Almacenado,
                Tratado = fila.Tratado,
                Acondicionado = fila.Acondicionado,
                Valorizado = fila.Valorizado,
                Comercializado = fila.Comercializado,
                DisposicionFinal = fila.DisposicionFinal,
            });
        }

        var resultado = await _repo.ObtenerAsync(declaracionId)
            ?? throw new AbrilException("No se pudo recuperar la declaración recalculada.", 500);
        return Proyectar(resultado);
    }

    public async Task<int> UpsertDetalleAsync(int declaracionId, ResiduoDeclaracionDetalleUpsertDto dto) =>
        await _repo.UpsertDetalleAsync(declaracionId, dto);

    public async Task EliminarDetalleAsync(int detalleId)
    {
        var ok = await _repo.EliminarDetalleAsync(detalleId);
        if (!ok) throw new AbrilException("Detalle no encontrado.", 404);
    }

    public async Task MarcarPresentadaAsync(int id, ResiduoDeclaracionMarcarPresentadaDto dto)
    {
        var ok = await _repo.MarcarPresentadaAsync(id, dto);
        if (!ok) throw new AbrilException("Declaración no encontrada.", 404);
    }

    public async Task VolverABorradorAsync(int id)
    {
        var ok = await _repo.VolverABorradorAsync(id);
        if (!ok) throw new AbrilException("Declaración no encontrada.", 404);
    }

    public async Task<string> SubirArchivoConstanciaAsync(int id, IFormFile archivo)
    {
        if (archivo.Length == 0) throw new AbrilException("El archivo está vacío.", 400);

        var container = _containerResolver.GetResiduosContainerName();
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";

        await using var stream = archivo.OpenReadStream();
        var uploadedUrls = await _fileStorageService.UploadFilesAsync(
            new[] { (Stream: (Stream)stream, FileName: fileName) }, container);
        var url = uploadedUrls.First();

        var ok = await _repo.MarcarPresentadaAsync(id, new ResiduoDeclaracionMarcarPresentadaDto
        {
            FechaPresentacion = DateOnly.FromDateTime(DateTime.UtcNow),
            ArchivoConstanciaUrl = url,
        });
        if (!ok) throw new AbrilException("Declaración no encontrada.", 404);
        return url;
    }

    public async Task EliminarAsync(int id)
    {
        var ok = await _repo.EliminarAsync(id);
        if (!ok) throw new AbrilException("Declaración no encontrada.", 404);
    }

    public Task<int> CrearEoRsIntervinienteAsync(int declaracionId, ResiduoDeclaracionEoRsUpsertDto dto) =>
        _repo.CrearEoRsIntervinienteAsync(declaracionId, dto);

    public async Task ActualizarEoRsIntervinienteAsync(int itemId, ResiduoDeclaracionEoRsUpsertDto dto)
    {
        var ok = await _repo.ActualizarEoRsIntervinienteAsync(itemId, dto);
        if (!ok) throw new AbrilException("Registro no encontrado.", 404);
    }

    public async Task EliminarEoRsIntervinienteAsync(int itemId)
    {
        var ok = await _repo.EliminarEoRsIntervinienteAsync(itemId);
        if (!ok) throw new AbrilException("Registro no encontrado.", 404);
    }
}
