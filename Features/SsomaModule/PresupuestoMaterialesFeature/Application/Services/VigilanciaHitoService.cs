using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

public class VigilanciaHitoService : IVigilanciaHitoService
{
    private readonly IVigilanciaHitoRepository _repo;
    public VigilanciaHitoService(IVigilanciaHitoRepository repo) => _repo = repo;

    public Task<List<VigilanciaHitoDto>> ObtenerPorProyectoAsync(int projectId)
        => _repo.ObtenerPorProyectoAsync(projectId);

    public Task<decimal?> ObtenerPrecioUnitarioActualAsync()
        => _repo.ObtenerPrecioUnitarioActualAsync();

    public Task GuardarAsync(int projectId, VigilanciaHitoGuardarDto dto, int userId)
        => _repo.GuardarAsync(projectId, dto.Items, userId);
}
