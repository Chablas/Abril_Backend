using Abril_Backend.Application.DTOs;
using Abril_Backend.Features.SsomaModule.OptFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.OptFeature.Application.Interfaces;

namespace Abril_Backend.Features.SsomaModule.OptFeature.Application.Services;

public class OptService : IOptService
{
    private readonly IOptRepository _repo;
    private readonly IOptSharePointService _sp;

    public OptService(IOptRepository repo, IOptSharePointService sp)
    {
        _repo = repo;
        _sp   = sp;
    }

    public Task<List<OptPetDto>> GetPetsAsync() => _repo.GetPetsAsync();

    public Task<List<OptCriterioVerificacionDto>> GetCriteriosVerificacionAsync()
        => _repo.GetCriteriosVerificacionAsync();

    public async Task<PagedResult<OptListItemDto>> GetListAsync(int? proyectoId, int? petId,
        string? tipoObservacion, DateTime? fechaDesde, DateTime? fechaHasta,
        int? trabajadorId, int page, int pageSize)
    {
        var items = await _repo.GetListAsync(
            proyectoId, petId, tipoObservacion, fechaDesde, fechaHasta, trabajadorId, page, pageSize);
        var total = await _repo.GetListCountAsync(
            proyectoId, petId, tipoObservacion, fechaDesde, fechaHasta, trabajadorId);

        return new PagedResult<OptListItemDto>
        {
            Data         = items,
            Page         = page,
            PageSize     = pageSize,
            TotalRecords = total,
            TotalPages   = (int)Math.Ceiling((double)total / pageSize)
        };
    }

    public async Task<OptDetalleDto> GetDetalleAsync(int id)
    {
        var detalle = await _repo.GetDetalleAsync(id);
        if (detalle == null) throw new KeyNotFoundException($"OPT {id} no encontrado.");
        return detalle;
    }

    public async Task<int> CrearOptAsync(CrearOptRequest request)
    {
        // 1. Crear OPT sin firmas para obtener el optId real
        var optId = await _repo.CrearOptAsync(request, null, new Dictionary<int, string>());

        // 2. Subir firmas usando el optId real (base64 → MemoryStream)
        string? firmaObservadorUrl = null;
        if (!string.IsNullOrEmpty(request.FirmaObservadorBase64))
        {
            var bytes = Convert.FromBase64String(request.FirmaObservadorBase64);
            using var stream = new MemoryStream(bytes);
            firmaObservadorUrl = await _sp.SubirFirmaObservadorAsync(stream, "firma_observador.png", optId);
        }

        var firmasTrabajadorUrls = new Dictionary<int, string>();
        foreach (var t in request.Trabajadores)
        {
            if (!string.IsNullOrEmpty(t.FirmaTrabajadorBase64))
            {
                var bytes = Convert.FromBase64String(t.FirmaTrabajadorBase64);
                using var stream = new MemoryStream(bytes);
                var url = await _sp.SubirFirmaTrabajadorAsync(
                    stream, $"firma_trabajador_{t.TrabajadorId}.png", optId, t.TrabajadorId);
                firmasTrabajadorUrls[t.TrabajadorId] = url;
            }
        }

        // 3. Actualizar URLs en DB si se subió alguna firma
        if (firmaObservadorUrl != null || firmasTrabajadorUrls.Count > 0)
            await _repo.UpdateFirmasAsync(optId, firmaObservadorUrl, firmasTrabajadorUrls);

        return optId;
    }

    public Task<OptDashboardDto> GetDashboardAsync(int? proyectoId, int? anio)
        => _repo.GetDashboardAsync(proyectoId, anio);
}
