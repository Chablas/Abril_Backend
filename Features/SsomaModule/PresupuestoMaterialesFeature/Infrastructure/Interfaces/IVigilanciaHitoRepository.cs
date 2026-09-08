using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

public interface IVigilanciaHitoRepository
{
    Task<List<VigilanciaHitoDto>> ObtenerPorProyectoAsync(int projectId);
    Task<decimal?> ObtenerPrecioUnitarioActualAsync();
    Task GuardarAsync(int projectId, List<VigilanciaHitoItemInputDto> items, int userId);
}
