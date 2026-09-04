using Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Application.Dtos;

namespace Abril_Backend.Features.CostsModule.Features.Configuration.PasosFeature.Infrastructure.Interfaces
{
    public interface ICostsPasoRepository
    {
        /// <summary>Pasos que tienen al menos una opción vigente, con sus opciones.</summary>
        Task<List<CostsPasoDto>> GetPasosAsync();

        /// <summary>Prende/apaga una opción. Devuelve false si la opción no existe o está dada de baja.</summary>
        Task<bool> UpdateOptionAsync(CostsPasoOptionUpdateDto dto, int userId);
    }
}
