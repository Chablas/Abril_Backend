using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Application.Dtos;
using Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Models;

namespace Abril_Backend.Features.AlmacenModule.Features.MaterialesFeature.Infrastructure.Interfaces;

public interface IMaterialRepository
{
    Task<AlmacenFiltrosDTO> GetFiltros();
    Task<List<AlmacenMaterialDTO>> GetMateriales(bool soloActivos);
    Task<AlmacenMaterialDTO> CreateMaterial(CreateAlmacenMaterialDTO body);
    Task<AlmacenMaterialDTO?> UpdateMaterial(int id, UpdateAlmacenMaterialDTO body);
    Task<bool> CodigoExiste(string codigo);
    Task<AlmacenMovimientoListResponseDTO> GetMovimientos(AlmacenMovimientosQueryParams query);
    Task<AlmacenMovimientoListItemDTO> CreateMovimiento(CreateAlmacenMovimientoDTO body, string? creadoPor);
    Task<AlmacenStockDTO> GetStock(int? proyectoId);
    Task<AlmacenDashboardDTO> GetDashboard(int? proyectoId, int diasVentana);

    Task<int?> ResolverProyectoIdPorNombre(string nombre);

    /// <summary>Busca el material por Código (case-insensitive); si no existe lo crea con el
    /// nombre/unidad de la fila importada. Devuelve su Id y si tuvo que crearlo.</summary>
    Task<(int MaterialId, bool Creado)> ResolverOCrearMaterial(string codigo, string nombre, string unidadMedida);

    /// <summary>Claves "proyectoId|materialId|fecha(yyyyMMdd)|tipo|cantidad" de movimientos ya
    /// existentes para los proyectos y rango de fechas del archivo — usado para no contabilizar
    /// dos veces la misma fila si el Excel se vuelve a importar.</summary>
    Task<HashSet<string>> ObtenerClavesMovimientosExistentes(List<int> proyectoIds, DateTime fechaMin, DateTime fechaMax);

    Task InsertarMovimientos(List<AlmacenMovimiento> movimientos);
}
