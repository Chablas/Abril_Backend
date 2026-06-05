namespace Abril_Backend.Features.MejoraContinuaModule.Features.LessonsLearnedFeature.Application.Interfaces
{
    /// <summary>
    /// Resuelve la jefatura para el flujo de aprobación de lecciones aprendidas.
    /// Reusa la idea de <c>ApproverResolver.ResolveApproverEmailAsync</c> (walk-up
    /// por el árbol <c>area_scope</c> a partir del <c>worker.area_scope_id</c>),
    /// pero SOLO considera hasta la categoría "Jefe" (los autores siempre son de
    /// categoría inferior a Jefe), y devuelve identidades (user_id) en lugar de correos.
    /// </summary>
    public interface ILessonJefeResolver
    {
        /// <summary>
        /// user_id del Jefe del autor (el "Jefe" más cercano caminando hacia arriba
        /// en el árbol de áreas). null si no se puede resolver.
        /// </summary>
        Task<int?> ResolveJefeUserIdAsync(int autorUserId);

        /// <summary>
        /// user_ids de los trabajadores cuyo Jefe-más-cercano es <paramref name="jefeUserId"/>.
        /// Usado por el filtro "Pendientes de mi revisión".
        /// </summary>
        Task<List<int>> GetSubordinateUserIdsAsync(int jefeUserId);
    }
}
