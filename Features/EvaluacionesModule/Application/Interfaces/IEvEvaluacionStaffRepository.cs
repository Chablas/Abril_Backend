using Abril_Backend.Features.Evaluaciones.Application.Dtos;

namespace Abril_Backend.Features.Evaluaciones.Application.Interfaces
{
    public interface IEvEvaluacionStaffRepository
    {
        /// <summary>
        /// true si el puesto actual del usuario es Residente (CategoriaIds.Residente) —
        /// mismo criterio que EvJefeSsomaRepository.ObtenerCategoriaPuestoAsync: app_user
        /// (por email) -> workers.email_corporativo -> workers.puesto_id -> puesto.categoria_id.
        /// </summary>
        Task<bool> EsResidenteAsync(int userId);

        /// <summary>
        /// project_id del proyecto vigente del Residente (worker_vinculaciones sin
        /// fecha_fin), o null si no tiene vinculación activa.
        /// </summary>
        Task<int?> ObtenerProyectoDeResidenteAsync(int userId);

        Task<List<EvEvaluacionStaffPendienteDto>> GetPendientesAsync(int evaluadorUserId, int periodoId);

        Task<List<EvStaffPlantillaCriterioDto>> GetPlantillaPorPuestoAsync(int puestoId);

        /// <summary>
        /// Registra la evaluación. Valida periodo activo, que el evaluado pertenezca al
        /// proyecto del residente evaluador y sea uno de los 16 puestos evaluables, los
        /// puntajes 1-5, y evita duplicado (UNIQUE periodo_id+evaluador_user_id+evaluado_worker_id).
        /// </summary>
        Task CreateAsync(
            int periodoId, int evaluadorUserId, int evaluadoWorkerId, int projectId,
            string? comentario, List<(int? plantillaId, string criterio, int puntaje)> detalles, decimal nota);

        Task<bool> YaEvaluoAsync(int periodoId, int evaluadorUserId, int evaluadoWorkerId);

        /// <summary>
        /// Valida que el trabajador evaluado esté activo, sea staff (obra_oficina_staff_id
        /// Staff), pertenezca al mismo proyecto del residente y su puesto esté en
        /// PuestoIds.StaffEvaluablePuestoIds. Devuelve su puesto_id si es válido.
        /// </summary>
        Task<int?> ValidarEvaluadoAsync(int evaluadoWorkerId, int projectId);

        Task<List<EvEvaluacionStaffResultadoDto>> GetResultadosAsync(int periodoId, int? projectId, int? puestoId);
    }
}
