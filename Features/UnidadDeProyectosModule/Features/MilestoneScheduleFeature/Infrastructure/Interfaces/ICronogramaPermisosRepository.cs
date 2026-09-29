namespace Abril_Backend.Features.UnidadDeProyectosModule.Features.MilestoneScheduleFeature.Infrastructure.Interfaces
{
    /// <summary>
    /// Quién puede tocar el cronograma de un proyecto. El residente de un proyecto es el Residente
    /// de Configuración → Proyectos (<c>project.residente_workers_id</c>), no la tabla antigua
    /// <c>project_resident</c>; la regla la da <c>IResidenteProyectoResolver</c>.
    /// </summary>
    public interface ICronogramaPermisosRepository
    {
        /// <summary>true si el usuario es el residente del proyecto.</summary>
        Task<bool> EsResidenteDelProyectoAsync(int userId, int projectId);

        /// <summary>true si alguno de sus roles tiene la feature de administrar el cronograma.</summary>
        Task<bool> AdministraAsync(int[] roleIds);

        /// <summary>
        /// true si puede modificar el cronograma del proyecto sin subir una versión nueva
        /// (culminar, marcar crítico, foto, característica): tiene el rol RESIDENTE y es el
        /// residente del proyecto, o tiene la feature de administrar. Una consulta, salvo para
        /// un RESIDENTE que no es el de ese proyecto (dos).
        /// </summary>
        Task<bool> PuedeEditarProyectoAsync(int userId, int[] roleIds, bool esResidente, int projectId);
    }
}
