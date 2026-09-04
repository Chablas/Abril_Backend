using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface IKitService
{
    Task<List<KitResumenDto>> ListarAsync(int? tipoId);
    Task<KitDetalleDto?> ObtenerAsync(int kitId);
    Task<int> CrearAsync(KitCreateDto dto);
    Task<List<KitCalculoLineaDto>> CalcularAsync(int kitId, decimal cantidadKits);
    Task<List<KitProyectoGuardadoDto>> ObtenerGuardadosPorProyectoAsync(int projectId);
    Task GuardarEnProyectoAsync(int projectId, KitProyectoGuardarDto dto, int userId);
    Task EliminarDelProyectoAsync(int projectId, int kitId);
}
