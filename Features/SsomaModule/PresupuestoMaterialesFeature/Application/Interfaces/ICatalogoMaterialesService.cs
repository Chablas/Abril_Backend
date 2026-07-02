using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface ICatalogoMaterialesService
{
    Task<SeedCatalogoResultDto> SeedCatalogoAsync(SeedCatalogoRequestDto request);
}
