using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface IEstandarizacionService
{
    Task<EstandarizacionLoteResultDto> EstandarizarCargaAsync(int cargaId);
}
