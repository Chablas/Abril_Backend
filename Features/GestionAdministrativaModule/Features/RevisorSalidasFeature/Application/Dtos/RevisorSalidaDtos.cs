namespace Abril_Backend.Features.GestionAdministrativa.RevisorSalidas.Application.Dtos
{
    /// <summary>
    /// Una fila por trabajador con email_corporativo @abril.pe, junto a su revisor de salidas
    /// directo (workers.worker_salida_jefe_id) encargado de aprobar/rechazar sus solicitudes
    /// de salida. Si no hay revisor asignado, el aprobador se resuelve por el árbol de áreas.
    /// </summary>
    public class WorkerRevisorSalidaItemDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public int? CategoryId { get; set; }
        public string? Category { get; set; }
        public int? JefeWorkerId { get; set; }
        public string? JefeFullName { get; set; }
        public string? JefeEmail { get; set; }
        public int? JefeCategoryId { get; set; }
        public string? JefeCategory { get; set; }
    }

    /// <summary>Opción del selector de revisor: worker con correo corporativo @abril.pe.</summary>
    public class WorkerRevisorSalidaOptionDto
    {
        public int WorkerId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }

    public class WorkerRevisorSalidaUpdateDto
    {
        public int? JefeWorkerId { get; set; }
    }
}
