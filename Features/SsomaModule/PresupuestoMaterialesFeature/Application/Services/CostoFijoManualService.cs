using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;
using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Services;

public class CostoFijoManualService : ICostoFijoManualService
{
    private readonly ICostoFijoManualRepository _repo;
    public CostoFijoManualService(ICostoFijoManualRepository repo) => _repo = repo;

    public async Task<CostoFijoManualDto> ObtenerPorProyectoAsync(int projectId) =>
        await _repo.ObtenerPorProyectoAsync(projectId)
        ?? new CostoFijoManualDto { ProjectId = projectId };

    public Task GuardarAsync(int projectId, ActualizarCostoFijoManualDto dto) =>
        _repo.GuardarAsync(projectId, dto);
}
