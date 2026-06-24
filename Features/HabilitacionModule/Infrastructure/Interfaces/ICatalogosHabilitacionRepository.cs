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
        Task<List<CatCategoria>> GetCategoriasAsync();
        Task<List<CatOcupacion>> GetOcupacionesAsync();

        // Categorías CRUD
        Task<List<CatCategoria>> GetCategoriasTodasAsync();
        Task<CatCategoria> CrearCategoriaAsync(string nombre);
        Task<CatCategoria> ActualizarCategoriaAsync(int id, string nombre);
        Task ToggleCategoriaAsync(int id, bool activo);

        // Ocupaciones CRUD
        Task<List<CatOcupacion>> GetOcupacionesTodasAsync();
        Task<CatOcupacion> CrearOcupacionAsync(string nombre);
        Task<CatOcupacion> ActualizarOcupacionAsync(int id, string nombre);
        Task ToggleOcupacionAsync(int id, bool activo);
    }
}
