using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Dtos;
using Microsoft.AspNetCore.Http;

namespace Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Interfaces;

public interface IMaterialService
{
    Task<AlmacenFiltrosDTO> GetFiltros();
    Task<List<AlmacenMaterialDTO>> GetMateriales(bool soloActivos);
    Task<AlmacenMaterialDTO> CreateMaterial(CreateAlmacenMaterialDTO body);
    Task<AlmacenMaterialDTO> UpdateMaterial(int id, UpdateAlmacenMaterialDTO body);
    Task<AlmacenMovimientoListResponseDTO> GetMovimientos(AlmacenMovimientosQueryParams query);
    Task<AlmacenMovimientoListItemDTO> CreateMovimiento(CreateAlmacenMovimientoDTO body, string? creadoPor);
    Task<AlmacenStockDTO> GetStock(int? proyectoId);
    Task<AlmacenDashboardDTO> GetDashboard(int? proyectoId, int diasVentana);
    Task<ImportarMovimientosResultDTO> ImportarMovimientos(IFormFile archivo, string? creadoPor);
}
