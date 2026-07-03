namespace Abril_Backend.Features.GestionAdministrativa.GestionSalidas.Application.Interfaces
{
    /// <summary>
    /// Resuelve el alcance de visibilidad de un usuario en la gestión de salidas:
    /// qué nodos <c>area_scope</c> puede ver (además de las solicitudes donde él es el
    /// aprobador). Primero mira el override manual (ga_salida_visibilidad_area); si el
    /// usuario no tiene ninguna asignación, cae al algoritmo de jerarquía.
    /// </summary>
    public interface ISalidaVisibilityResolver
    {
        Task<SalidaVisibility> ResolveAsync(int userId);
    }

    /// <summary>
    /// Resultado de la resolución de visibilidad.
    ///   • <see cref="SeesAll"/> = true  → ve TODAS las solicitudes (sin restricción por área).
    ///   • <see cref="AreaScopeIds"/>     → conjunto de nodos cuyos trabajadores puede ver.
    /// En ambos casos, además, el usuario siempre ve las solicitudes en las que él es el
    /// aprobador resuelto (eso se aplica aparte, en el repositorio).
    /// </summary>
    public record SalidaVisibility(bool SeesAll, HashSet<int> AreaScopeIds);
}
