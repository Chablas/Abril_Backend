using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface IRatioService
{
    Task<CalcularRatiosResultDto> CalcularRatiosProyectoAsync(int projectId);
    Task<List<RatioProyectoDto>> ObtenerRatiosProyectoAsync(int projectId);
    Task<RatioFamiliaComparacionDto?> ObtenerComparacionFamiliaAsync(int familiaId);
    Task<ResumenRatiosDto> ObtenerResumenAsync();
}
