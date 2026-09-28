namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces
{
    /// <summary>
    /// Quién puede tocar el cronograma de un proyecto. El residente de un proyecto es el que
    /// figura en Configuración → Proyectos → Emails SSOMA (<c>project.residente_workers_id</c>),
    /// no la tabla antigua <c>project_resident</c>.
    /// </summary>
    public interface ICronogramaPermisosRepository
    {
        /// <summary>true si el usuario es el residente del proyecto en Emails SSOMA.</summary>
        Task<bool> EsResidenteDelProyectoAsync(int userId, int projectId);

        /// <summary>true si alguno de sus roles tiene la feature de administrar el cronograma.</summary>
        Task<bool> AdministraAsync(int[] roleIds);

        /// <summary>
        /// true si puede modificar el cronograma del proyecto sin subir una versión nueva
        /// (culminar, marcar crítico, foto, característica): tiene la feature de administrar, o
        /// tiene el rol RESIDENTE y es el residente del proyecto. Una sola consulta.
        /// </summary>
        Task<bool> PuedeEditarProyectoAsync(int userId, int[] roleIds, bool esResidente, int projectId);
    }
}
