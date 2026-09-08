using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface IVigilanciaHitoService
{
    Task<List<VigilanciaHitoDto>> ObtenerPorProyectoAsync(int projectId);
    Task<decimal?> ObtenerPrecioUnitarioActualAsync();
    Task GuardarAsync(int projectId, VigilanciaHitoGuardarDto dto, int userId);
}
