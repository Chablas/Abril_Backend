using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface IServicioFijoService
{
    Task<List<FamiliaFijaDisponibleDto>> ObtenerFamiliasFijasDisponiblesAsync();
    Task<List<ServicioFijoDto>> ObtenerPorProyectoAsync(int projectId);
    Task GuardarAsync(int projectId, ServiciosFijosGuardarDto dto, int userId);
}
