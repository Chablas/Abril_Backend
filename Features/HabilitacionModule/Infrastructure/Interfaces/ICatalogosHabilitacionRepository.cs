using Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;

namespace Abril_Backend.Features.Habilitacion.Infrastructure.Interfaces
{
    public interface ICatalogosHabilitacionRepository
    {
        Task<List<SsItemTrabajador>> GetItemsTrabajadorAsync();
        Task<List<SsItemEmpresa>> GetItemsEmpresaAsync();
        Task<List<SsItemEquipo>> GetItemsEquipoAsync();
        Task<List<SsCriterioEvaluacion>> GetCriteriosEvaluacionAsync();

        /// <summary>
        /// Árbol de áreas con la equivalencia legacy y el revisor resueltos por nodo. Reemplaza a
        /// GetAreasAsync/GetSubareasAsync en el formulario de trabajadores, que ahora elige el nodo
        /// del árbol en vez de los textos area/subarea.
        /// </summary>
        Task<List<AreaArbolNodoDto>> GetAreaArbolAsync();

        /// <summary>
        /// Catálogo Obra / Staff / Oficina Central (workers_obra_oficina_staff). Es el
        /// desplegable que define la ubicación del trabajador; antes esa distinción se
        /// deducía del último nodo del árbol de áreas (tipo "Área Obra_Oficina").
        /// </summary>
        Task<List<ObraOficinaStaffDto>> GetObraOficinaStaffAsync();

        /// <summary>Áreas del catálogo legacy cat_subarea. Sigue vivo para otros consumidores.</summary>
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
