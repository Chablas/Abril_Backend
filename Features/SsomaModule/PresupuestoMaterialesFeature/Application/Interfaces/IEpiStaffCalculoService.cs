using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Interfaces;

public interface IEpiStaffCalculoService
{
    Task<EpiStaffConfigDto> ObtenerConfigAsync();
    Task ActualizarConfigAsync(ActualizarEpiStaffConfigDto dto);

    /// <summary>Null si el proyecto todavía no tiene ningún presupuesto generado.</summary>
    Task<EpiStaffCalculoDto?> CalcularAsync(int projectId);
}
