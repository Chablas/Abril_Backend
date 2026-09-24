namespace Abril_Backend.Features.PlaneamientoBimFeature.Application.Interfaces
{
    /// <summary>Autorización a nivel de proyecto para las 4 pestañas de Planeamiento BIM que
    /// reciben projectId/restriccionId por parámetro (Configuración Inicial, Carga Diaria,
    /// Restricciones, Dashboard). [RequireFeature] solo valida acceso al feature en general —
    /// esto valida que el projectId puntual de la request pertenece al usuario, para que un
    /// PLANEAMIENTO_UDP sin ese proyecto asignado no pueda leer/escribir datos ajenos pasando
    /// el Id a mano (Postman/DevTools), aunque su propio dropdown nunca se lo haya mostrado.</summary>
    public interface IPlaneamientoBimAccesoService
    {
        /// <summary>Lanza AbrilException(403) si el usuario no tiene acceso a projectId.
        /// Administrador de Sistema/UDP: acceso a cualquier proyecto. Planeamiento UDP: solo
        /// si es Project.ResponsablePlaneamientoBimId. Ningún otro rol: siempre rechazado.</summary>
        Task ValidarAccesoProyecto(int userId, int projectId, bool esAdmin, bool esPlaneamientoUdp);

        /// <summary>Resuelve el ProjectId real de una restricción (bim_bloqueo) a partir de su
        /// Id, para poder validar acceso en Update/Cerrar — esos 2 endpoints no reciben
        /// projectId en la ruta. Lanza AbrilException(404) si la restricción no existe.</summary>
        Task<int> ResolverProjectIdDeRestriccion(int restriccionId);
    }
}
