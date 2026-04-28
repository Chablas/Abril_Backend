using Abril_Backend.Features.Habilitacion.Infrastructure.Models;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface ICatalogosHabilitacionRepository
    {
        Task<List<SsItemTrabajador>> GetItemsTrabajadorAsync();
        Task<List<SsItemEmpresa>> GetItemsEmpresaAsync();
        Task<List<SsItemEquipo>> GetItemsEquipoAsync();
        Task<List<SsCriterioEvaluacion>> GetCriteriosEvaluacionAsync();
    }
}
