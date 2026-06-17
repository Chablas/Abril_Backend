using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Interfaces;

public interface IInspeccionRepository
{
    Task<List<InspeccionTipoDto>> GetTiposAsync();
    Task<List<InspeccionChecklistItemDto>> GetChecklistItemsAsync(int tipoId);
    Task<List<InspeccionListItemDto>> GetListAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize);
    Task<int> GetListCountAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta);
    Task<InspeccionDetalleDto?> GetDetalleAsync(int id);
    Task<int> CrearInspeccionAsync(CrearInspeccionRequest request,
        string? firmaInspectorUrl, string? firmaRepresentanteUrl,
        Dictionary<int, List<string>> fotosHallazgoUrls);
    Task CerrarHallazgoAsync(int hallazgoId, CerrarHallazgoRequest request, string? evidenciaUrl);
    Task ActualizarFirmasYFotosAsync(int id, string? firmaInspectorUrl, string? firmaRepresentanteUrl, Dictionary<int, List<string>> fotosHallazgoUrls);
    Task<InspeccionDashboardDto> GetDashboardAsync(int? proyectoId, int? anio);
    Task<List<HallazgoListItemDto>> GetHallazgosAsync(string? estado, string? proyecto, string? area, DateTime? fechaLimiteHasta);
    Task LevantarHallazgoAsync(int hallazgoId, LevantarHallazgoDto dto);
}
