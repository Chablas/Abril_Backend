using Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Application.Dtos;

namespace Abril_Backend.Features.SsomaModule.PresupuestoMaterialesFeature.Infrastructure.Interfaces;

public interface ICostoFijoManualRepository
{
    /// <summary>Null si el proyecto todavía no tiene nada guardado — el servicio lo traduce a
    /// montos en cero, no hay que tratar el null en la UI.</summary>
    Task<CostoFijoManualDto?> ObtenerPorProyectoAsync(int projectId);
    Task GuardarAsync(int projectId, ActualizarCostoFijoManualDto dto);
}
