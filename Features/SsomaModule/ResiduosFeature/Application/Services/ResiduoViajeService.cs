using Microsoft.AspNetCore.Http;
using Abril_Backend.Application.Exceptions;
using Abril_Backend.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Interfaces;
using Abril_Backend.Features.SsomaModule.ResiduosFeature.Infrastructure.Models;

namespace Abril_Backend.Features.SsomaModule.ResiduosFeature.Application.Services;

public class ResiduoViajeService : IResiduoViajeService
{
    private readonly IResiduoViajeRepository _repo;
    private readonly IResiduoTipoRepository _tipoRepo;
    private readonly IFileStorageService _fileStorageService;
    private readonly IStorageContainerResolver _containerResolver;

    public ResiduoViajeService(
        IResiduoViajeRepository repo,
        IResiduoTipoRepository tipoRepo,
        IFileStorageService fileStorageService,
        IStorageContainerResolver containerResolver)
    {
        _repo = repo;
        _tipoRepo = tipoRepo;
        _fileStorageService = fileStorageService;
        _containerResolver = containerResolver;
    }

    public Task<ResiduoViajePagedDto> ListarAsync(ResiduoViajeListFiltroDto filtro) => _repo.ListarAsync(filtro);

    public async Task<ResiduoViajeDto?> ObtenerAsync(long id)
    {
        var entidad = await _repo.ObtenerAsync(id);
        if (entidad == null) return null;
        return new ResiduoViajeDto
        {
            Id = entidad.Id,
            ProjectId = entidad.ProjectId,
            AutorizacionDmeId = entidad.AutorizacionDmeId,
            ResiduoTipoId = entidad.ResiduoTipoId,
            NombreResiduoTipo = entidad.ResiduoTipo?.Nombre,
            EoRsId = entidad.EoRsId,
            NombreEoRs = entidad.EoRs?.RazonSocial,
            Contratista = entidad.Contratista,
            Origen = entidad.Origen,
            Fecha = entidad.Fecha,
            CantidadM3 = entidad.CantidadM3,
            CantidadTon = entidad.CantidadTon,
            TipoManejo = entidad.TipoManejo,
            GestorReceptor = entidad.GestorReceptor,
            DestinoFinal = entidad.DestinoFinal,
            NumeroRegistro = entidad.NumeroRegistro,
            NumeroCertificado = entidad.NumeroCertificado,
            ArchivoUrl = entidad.ArchivoUrl,
            Activo = entidad.Activo,
        };
    }

    private static void Validar(ResiduoViajeUpsertDto dto)
    {
        if (dto.CantidadM3 <= 0)
            throw new AbrilException("La cantidad en m3 debe ser mayor a 0.", 400);
        if (string.IsNullOrWhiteSpace(dto.TipoManejo))
            throw new AbrilException("El tipo de manejo es obligatorio.", 400);
    }

    /// <summary>
    /// Cálculo automático de toneladas: si el usuario no especifica cantidad_ton manualmente, se
    /// calcula como cantidad_m3 * factor vigente del tipo de residuo para la fecha del viaje. Si no
    /// hay factor vigente, se deja cantidad_ton en null sin fallar.
    /// </summary>
    private async Task<decimal?> CalcularCantidadTonAsync(ResiduoViajeUpsertDto dto)
    {
        if (dto.CantidadTon.HasValue) return dto.CantidadTon;

        var factor = await _tipoRepo.ObtenerFactorVigenteAsync(dto.ResiduoTipoId, dto.Fecha);
        return factor.HasValue ? Math.Round(dto.CantidadM3 * factor.Value, 3) : null;
    }

    public async Task<long> CrearAsync(ResiduoViajeUpsertDto dto, int userId)
    {
        Validar(dto);
        var cantidadTon = await CalcularCantidadTonAsync(dto);

        var entidad = new SsResiduoViaje
        {
            ProjectId = dto.ProjectId,
            AutorizacionDmeId = dto.AutorizacionDmeId,
            ResiduoTipoId = dto.ResiduoTipoId,
            EoRsId = dto.EoRsId,
            Contratista = dto.Contratista,
            Origen = dto.Origen,
            Fecha = dto.Fecha,
            CantidadM3 = dto.CantidadM3,
            CantidadTon = cantidadTon,
            TipoManejo = dto.TipoManejo,
            GestorReceptor = dto.GestorReceptor,
            DestinoFinal = dto.DestinoFinal,
            NumeroRegistro = dto.NumeroRegistro,
            NumeroCertificado = dto.NumeroCertificado,
            CreadoPor = userId,
        };
        return await _repo.CrearAsync(entidad);
    }

    public async Task ActualizarAsync(long id, ResiduoViajeUpsertDto dto)
    {
        Validar(dto);
        var cantidadTon = await CalcularCantidadTonAsync(dto);

        var entidad = new SsResiduoViaje
        {
            Id = id,
            ProjectId = dto.ProjectId,
            AutorizacionDmeId = dto.AutorizacionDmeId,
            ResiduoTipoId = dto.ResiduoTipoId,
            EoRsId = dto.EoRsId,
            Contratista = dto.Contratista,
            Origen = dto.Origen,
            Fecha = dto.Fecha,
            CantidadM3 = dto.CantidadM3,
            CantidadTon = cantidadTon,
            TipoManejo = dto.TipoManejo,
            GestorReceptor = dto.GestorReceptor,
            DestinoFinal = dto.DestinoFinal,
            NumeroRegistro = dto.NumeroRegistro,
            NumeroCertificado = dto.NumeroCertificado,
        };
        var ok = await _repo.ActualizarAsync(entidad);
        if (!ok) throw new AbrilException("Viaje no encontrado.", 404);
    }

    public async Task DesactivarAsync(long id)
    {
        var ok = await _repo.DesactivarAsync(id);
        if (!ok) throw new AbrilException("Viaje no encontrado.", 404);
    }

    public async Task<string> SubirArchivoAsync(long id, IFormFile archivo)
    {
        if (archivo.Length == 0) throw new AbrilException("El archivo está vacío.", 400);

        var container = _containerResolver.GetResiduosContainerName();
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(archivo.FileName)}";

        await using var stream = archivo.OpenReadStream();
        var uploadedUrls = await _fileStorageService.UploadFilesAsync(
            new[] { (Stream: (Stream)stream, FileName: fileName) }, container);
        var url = uploadedUrls.First();

        var ok = await _repo.SetArchivoAsync(id, url);
        if (!ok) throw new AbrilException("Viaje no encontrado.", 404);
        return url;
    }
}
