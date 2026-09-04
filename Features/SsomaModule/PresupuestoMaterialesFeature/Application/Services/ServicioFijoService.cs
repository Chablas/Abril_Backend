using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

public class ServicioFijoService : IServicioFijoService
{
    private readonly IServicioFijoRepository _repo;
    public ServicioFijoService(IServicioFijoRepository repo) => _repo = repo;

    public Task<List<FamiliaFijaDisponibleDto>> ObtenerFamiliasFijasDisponiblesAsync()
        => _repo.ObtenerFamiliasFijasDisponiblesAsync();

    public Task<List<ServicioFijoDto>> ObtenerPorProyectoAsync(int projectId)
        => _repo.ObtenerPorProyectoAsync(projectId);

    public Task GuardarAsync(int projectId, ServiciosFijosGuardarDto dto, int userId)
        => _repo.GuardarAsync(projectId, dto.Items, userId);
}
