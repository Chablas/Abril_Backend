using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Interfaces;

public interface IMaterialRepository
{
    Task<AlmacenFiltrosDTO> GetFiltros();
    Task<AlmacenMaterialDTO> CreateMaterial(CreateAlmacenMaterialDTO body);
    Task<bool> CodigoExiste(string codigo);
    Task<AlmacenMovimientoListResponseDTO> GetMovimientos(AlmacenMovimientosQueryParams query);
    Task<AlmacenMovimientoListItemDTO> CreateMovimiento(CreateAlmacenMovimientoDTO body, string? creadoPor);
    Task<AlmacenStockDTO> GetStock(int? proyectoId);
    Task<AlmacenDashboardDTO> GetDashboard(int? proyectoId, int diasVentana);
}
