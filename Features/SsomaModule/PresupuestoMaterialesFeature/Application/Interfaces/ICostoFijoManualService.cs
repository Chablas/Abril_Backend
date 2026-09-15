using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface ICostoFijoManualService
{
    Task<CostoFijoManualDto> ObtenerPorProyectoAsync(int projectId);
    Task GuardarAsync(int projectId, ActualizarCostoFijoManualDto dto);
}
