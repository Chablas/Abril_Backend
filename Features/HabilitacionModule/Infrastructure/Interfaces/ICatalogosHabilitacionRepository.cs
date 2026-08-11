using Abril_Backend.Features.Habilitacion.Application.Dtos.Catalogos;
using Abril_Backend.Features.Habilitacion.Infrastructure.Models;
using Abril_Backend.Shared.Models;

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
        /// <summary>Categorías activas: el campo de lógica del trabajador.</summary>
        Task<List<Categoria>> GetCategoriasAsync();

        /// <summary>Puestos activos: el campo de presentación. Cada uno apunta a una categoría.</summary>
        Task<List<Puesto>> GetPuestosAsync();

        // Categorías CRUD
        Task<List<Categoria>> GetCategoriasTodasAsync();
        Task<Categoria> CrearCategoriaAsync(string nombre);
        Task<Categoria> ActualizarCategoriaAsync(int id, string nombre);
        Task ToggleCategoriaAsync(int id, bool activo);

        // Puestos CRUD
        Task<List<Puesto>> GetPuestosTodosAsync();
        Task<Puesto> CrearPuestoAsync(string nombre, int? categoriaId);
        Task<Puesto> ActualizarPuestoAsync(int id, string nombre, int? categoriaId);
        Task TogglePuestoAsync(int id, bool activo);
    }
}
