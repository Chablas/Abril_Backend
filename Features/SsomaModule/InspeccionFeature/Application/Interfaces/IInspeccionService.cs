using Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.InspeccionFeature.Application.Interfaces;

public interface IInspeccionService
{
    Task<object> GetCatalogosAsync();
    Task<List<InspeccionChecklistItemDto>> GetChecklistAsync(int tipoId);
    Task<object> GetListAsync(int? proyectoId, int? tipoId,
        string? estado, DateTime? fechaDesde, DateTime? fechaHasta, int page, int pageSize,
        int? empresaIdContratista = null);
    Task<InspeccionDetalleDto> GetDetalleAsync(int id);
    Task<int> CrearInspeccionAsync(CrearInspeccionRequest request, int? userId = null);
    Task CerrarHallazgoAsync(int hallazgoId, CerrarHallazgoRequest request);
    Task<InspeccionDashboardDto> GetDashboardAsync(int? proyectoId, int? anio, int? empresaIdContratista = null);
    Task<List<HallazgoListItemDto>> GetHallazgosAsync(string? estado, string? proyecto, string? area, DateTime? fechaLimiteHasta, int? empresaIdContratista = null);
    Task LevantarHallazgoAsync(int hallazgoId, LevantarHallazgoDto dto);
    Task<(int? EmpresaId, int? EmpresaInspectoraId)> GetEmpresaIdDeHallazgoAsync(int hallazgoId);
}
