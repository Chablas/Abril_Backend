using Abril_Backend.Application.Exceptions;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Interfaces;

namespace Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Services;

public class InspeccionService : IInspeccionService
{
    private readonly IInspeccionRepository _repo;
    private readonly IInspeccionSharePointService _sp;

    public InspeccionService(IInspeccionRepository repo, IInspeccionSharePointService sp)
    {
        _repo = repo;
        _sp = sp;
    }

    public async Task<object> GetCatalogosAsync()
    {
        var tipos = await _repo.GetTiposAsync();
        return new { tipos };
    }

    public async Task<List<InspeccionChecklistItemDto>> GetChecklistAsync(int tipoId)
        => await _repo.GetChecklistItemsAsync(tipoId);

    public async Task<object> GetListAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize)
    {
        var items = await _repo.GetListAsync(proyectoId, tipoId, estado, fechaDesde, fechaHasta, page, pageSize);
        var total = await _repo.GetListCountAsync(proyectoId, tipoId, estado, fechaDesde, fechaHasta);
        return new { items, total, page, pageSize };
    }

    public async Task<InspeccionDetalleDto> GetDetalleAsync(int id)
    {
        var result = await _repo.GetDetalleAsync(id);
        if (result == null) throw new AbrilException("Inspección no encontrada.", 404);
        return result;
    }

    public async Task<InspeccionDashboardDto> GetDashboardAsync(int? proyectoId, int? anio)
        => await _repo.GetDashboardAsync(proyectoId, anio);

    public async Task<int> CrearInspeccionAsync(CrearInspeccionRequest request)
    {
        if (request.TipoId <= 0)
            throw new AbrilException("El tipo de inspección es requerido.", 400);

        // PASO 1: Crear inspección sin firmas para obtener el ID
        var fotosHallazgoUrls = new Dictionary<int, List<string>>();
        var id = await _repo.CrearInspeccionAsync(request, null, null, fotosHallazgoUrls);

        // PASO 2: Subir firmas y fotos con el ID real
        string? firmaInspectorUrl = null;
        if (!string.IsNullOrEmpty(request.FirmaInspectorBase64))
        {
            var bytes = Convert.FromBase64String(
                request.FirmaInspectorBase64.Contains(",")
                    ? request.FirmaInspectorBase64.Split(',')[1]
                    : request.FirmaInspectorBase64);
            using var stream = new MemoryStream(bytes);
            firmaInspectorUrl = await _sp.SubirFirmaInspectorAsync(
                stream, $"inspector_{DateTime.UtcNow:yyyyMMddHHmmss}.png", id);
        }

        string? firmaRepresentanteUrl = null;
        if (!string.IsNullOrEmpty(request.FirmaRepresentanteBase64))
        {
            var bytes = Convert.FromBase64String(
                request.FirmaRepresentanteBase64.Contains(",")
                    ? request.FirmaRepresentanteBase64.Split(',')[1]
                    : request.FirmaRepresentanteBase64);
            using var stream = new MemoryStream(bytes);
            firmaRepresentanteUrl = await _sp.SubirFirmaRepresentanteAsync(
                stream, $"representante_{DateTime.UtcNow:yyyyMMddHHmmss}.png", id);
        }

        for (int i = 0; i < request.Hallazgos.Count; i++)
        {
            var urls = new List<string>();
            for (int j = 0; j < request.Hallazgos[i].FotosBase64.Count; j++)
            {
                var base64 = request.Hallazgos[i].FotosBase64[j];
                var data = base64.Contains(",") ? base64.Split(',')[1] : base64;
                var bytes = Convert.FromBase64String(data);
                using var stream = new MemoryStream(bytes);
                var url = await _sp.SubirFotoHallazgoAsync(
                    stream, $"foto_{i}_{j}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg", id, i);
                urls.Add(url);
            }
            if (urls.Any()) fotosHallazgoUrls[i] = urls;
        }

        // PASO 3: Actualizar con firmas y fotos
        if (firmaInspectorUrl != null || firmaRepresentanteUrl != null || fotosHallazgoUrls.Any())
            await _repo.ActualizarFirmasYFotosAsync(id, firmaInspectorUrl, firmaRepresentanteUrl, fotosHallazgoUrls);

        return id;
    }

    public async Task CerrarHallazgoAsync(int hallazgoId, CerrarHallazgoRequest request)
    {
        string? evidenciaUrl = null;
        if (!string.IsNullOrEmpty(request.EvidenciaCierreBase64))
        {
            var data = request.EvidenciaCierreBase64.Contains(",")
                ? request.EvidenciaCierreBase64.Split(',')[1]
                : request.EvidenciaCierreBase64;
            var bytes = Convert.FromBase64String(data);
            using var stream = new MemoryStream(bytes);
            evidenciaUrl = await _sp.SubirFotoHallazgoAsync(
                stream, $"evidencia_{hallazgoId}_{DateTime.UtcNow:yyyyMMddHHmmss}.jpg",
                0, hallazgoId);
        }
        await _repo.CerrarHallazgoAsync(hallazgoId, request, evidenciaUrl);
    }
}
