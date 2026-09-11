using Abril_Backend.Features.GestionAdministrativa.Shared.Dtos;
using Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Application.Dtos;

namespace Abril_Backend.Features.GestionAdministrativa.VisibilidadAreas.Infrastructure.Interfaces
{
    /// <summary>
    /// Override manual de "qué áreas ve este trabajador", por ámbito
    /// (<c>Abril_Backend.Shared.Constants.VisibilidadAmbitoIds</c>): Gestión de Salidas y Gestión
    /// de Rendiciones tienen cada una la suya sobre la misma tabla.
    /// </summary>
    public interface IVisibilidadAreaRepository
    {
        /// <summary>Carga inicial: trabajadores (tabla) + árbol de áreas (filtro), en una sola conexión.</summary>
        Task<VisibilidadInicialDto> GetInitialDataAsync(int ambitoId);

        /// <summary>Árbol de áreas (lista plana de nodos vivos).</summary>
        Task<List<GaAreaNodeDto>> GetAreaTreeAsync();

        /// <summary>Asignaciones vivas de un trabajador en ese ámbito.</summary>
        Task<List<VisibilidadAsignacionDto>> GetWorkerAsignacionesAsync(int ambitoId, int workerId);

        /// <summary>Reemplaza el conjunto de asignaciones de un trabajador en ese ámbito.</summary>
        Task UpdateWorkerAsignacionesAsync(int ambitoId, int workerId, List<VisibilidadAsignacionDto> asignaciones);
    }
}
