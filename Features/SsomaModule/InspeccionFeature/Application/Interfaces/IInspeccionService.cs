using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Interfaces;

public interface IInspeccionService
{
    Task<object> GetCatalogosAsync();
    Task<List<InspeccionChecklistItemDto>> GetChecklistAsync(int tipoId);
    Task<object> GetListAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize);
    Task<InspeccionDetalleDto> GetDetalleAsync(int id);
    Task<int> CrearInspeccionAsync(CrearInspeccionRequest request);
    Task CerrarHallazgoAsync(int hallazgoId, CerrarHallazgoRequest request);
    Task<InspeccionDashboardDto> GetDashboardAsync(int? proyectoId, int? anio);
    Task<List<HallazgoListItemDto>> GetHallazgosAsync(string? estado, string? proyecto, string? area, DateTime? fechaLimiteHasta);
    Task LevantarHallazgoAsync(int hallazgoId, LevantarHallazgoDto dto);
}
