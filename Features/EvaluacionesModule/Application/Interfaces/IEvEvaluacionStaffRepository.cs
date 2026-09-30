using Abril_Backend.Features.Evaluaciones.Application.Dtos;

namespace Abril_Backend.Features.Evaluaciones.Application.Interfaces
{
    public interface IEvEvaluacionStaffRepository
    {
        /// <summary>
        /// true si el usuario evalúa staff: es el residente de alguna obra
        /// (<see cref="EsResidenteDeObraAsync"/>) o es el Jefe SSOMA (por puesto único, cruzando
        /// app_user -> workers.email_corporativo, como EvGestionSsomaRepository.EsJefeSsomaAsync).
        /// </summary>
        Task<bool> EsResidenteAsync(int userId);

        /// <summary>
        /// true si el usuario es el residente de alguna obra en Configuración → Proyectos y tiene
        /// el rol RESIDENTE (ResidenteQueries.ObrasConSuResidente). El puesto no decide.
        /// </summary>
        Task<bool> EsResidenteDeObraAsync(int userId);

        /// <summary>
        /// Proyectos cuyo staff evalúa el usuario: las obras donde es el residente en
        /// Configuración → Proyectos o, si no es residente de ninguna, el proyecto vigente del
        /// Jefe SSOMA (worker_vinculaciones sin fecha_fin). Vacía si no evalúa staff.
        /// </summary>
        Task<List<int>> ObtenerProyectosDelEvaluadorAsync(int userId);

        Task<List<EvEvaluacionStaffPendienteDto>> GetPendientesAsync(int evaluadorUserId, int periodoId);

        Task<List<EvStaffPlantillaCriterioDto>> GetPlantillaPorPuestoAsync(int puestoId);

        /// <summary>
        /// Registra la evaluación. Valida periodo activo, que el evaluado pertenezca a una de
        /// las obras del residente evaluador y sea uno de los 16 puestos evaluables, los
        /// puntajes 1-5, y evita duplicado (UNIQUE periodo_id+evaluador_user_id+evaluado_worker_id).
        /// </summary>
        Task CreateAsync(
            int periodoId, int evaluadorUserId, int evaluadoWorkerId, int projectId,
            string? comentario, List<(int? plantillaId, string criterio, int puntaje)> detalles, decimal nota);

        Task<bool> YaEvaluoAsync(int periodoId, int evaluadorUserId, int evaluadoWorkerId);

        /// <summary>
        /// Valida que el trabajador evaluado esté activo, sea staff (obra_oficina_staff_id
        /// Staff), esté vinculado a uno de los proyectos del evaluador y su puesto esté en
        /// PuestoIds.StaffEvaluablePuestoIds. Devuelve ese proyecto (el de la evaluación) si es
        /// válido, o null.
        /// </summary>
        Task<int?> ValidarEvaluadoAsync(int evaluadoWorkerId, List<int> projectIds);

        Task<List<EvEvaluacionStaffResultadoDto>> GetResultadosAsync(int periodoId, int? projectId, int? puestoId);
    }
}
