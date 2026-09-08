using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

public interface IServicioFijoRepository
{
    Task<List<FamiliaFijaDisponibleDto>> ObtenerFamiliasFijasDisponiblesAsync();
    Task<List<ServicioFijoDto>> ObtenerPorProyectoAsync(int projectId);
    Task GuardarAsync(int projectId, List<ServicioFijoItemInputDto> items, int userId);
}
