using Abril_Backend.Features.Habilitacion.Infrastructure.Models;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface ICatalogosHabilitacionRepository
    {
        Task<List<SsItemTrabajador>> GetItemsTrabajadorAsync();
        Task<List<SsItemEmpresa>> GetItemsEmpresaAsync();
        Task<List<SsItemEquipo>> GetItemsEquipoAsync();
        Task<List<SsCriterioEvaluacion>> GetCriteriosEvaluacionAsync();
        Task<List<string>> GetAreasAsync();
        Task<List<CatSubarea>> GetSubareasAsync(string? area);
    }
}
