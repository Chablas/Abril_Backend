namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces
{
    /// <summary>
    /// Resuelve el alcance de visibilidad de un usuario en una de las dos bandejas del flujo:
    /// qué nodos <c>area_scope</c> puede ver (además de lo suyo y de lo que le toca decidir). El
    /// ámbito (<c>Abril_Backend.Shared.Constants.VisibilidadAmbitoIds</c>) dice de cuál se está
    /// preguntando: Gestión de Salidas o Gestión de Rendiciones.
    ///
    /// Las dos comparten el algoritmo de jerarquía pero NO la configuración: cada una tiene sus
    /// propias filas de override en <c>ga_visibilidad_area</c>, porque ver todas las solicitudes de
    /// salida y ver todas las planillas de rendición son dos permisos distintos.
    ///
    /// Al conjunto siempre se le suman los nodos (con su subárbol) donde el usuario está designado
    /// como revisor de área (<c>area_revisores</c>) y, en el ámbito de rendiciones, también donde
    /// está designado como consolidador (<c>area_consolidadores</c>): a esas ramas les tiene que
    /// poder hacer el trabajo que se le asignó. Sobre ese piso, primero manda el override manual y,
    /// si el usuario no tiene ninguna asignación en ese ámbito, el algoritmo de jerarquía.
    /// </summary>
    public interface ISalidaVisibilityResolver
    {
        /// <param name="ambitoId">Ver <c>VisibilidadAmbitoIds</c>: Salidas o Rendiciones.</param>
        Task<SalidaVisibility> ResolveAsync(int userId, int ambitoId);
    }

    /// <summary>
    /// Resultado de la resolución de visibilidad.
    ///   • <see cref="SeesAll"/> = true  → ve TODAS las solicitudes (sin restricción por área).
    ///   • <see cref="AreaScopeIds"/>     → conjunto de nodos cuyos trabajadores puede ver.
    /// </summary>
    public record SalidaVisibility(bool SeesAll, HashSet<int> AreaScopeIds);
}
