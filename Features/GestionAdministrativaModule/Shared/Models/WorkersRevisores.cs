using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Revisores de salidas por trabajador: cada solicitante puede tener n posibles
    /// revisores ordenados por prioridad (1 = mayor prioridad). Al crear una solicitud
    /// de salida se envía el correo al primer revisor vivo (state) y activo (active)
    /// con correo corporativo @abril.pe; si no hay ninguno, se hace fallback al área
    /// de GTH (area_scope.email del área "Gestión del Talento Humano").
    /// Reemplaza al campo 1:1 workers.worker_salida_jefe_id (que queda sin uso) y al
    /// algoritmo de jerarquía ApproverResolver (JefeResolver, también sin uso).
    /// </summary>
    [Table("workers_revisores")]
    public class WorkersRevisores
    {
        [Column("workers_revisores_id")]
        public int WorkersRevisoresId { get; set; }

        /// <summary>Trabajador (workers.id) que pide la salida.</summary>
        [Column("solicitante_id")]
        public int SolicitanteId { get; set; }

        /// <summary>Trabajador (workers.id) que revisa/aprueba las salidas del solicitante.</summary>
        [Column("revisor_id")]
        public int RevisorId { get; set; }

        /// <summary>1 = primero en ser considerado; a mayor número, menor prioridad.</summary>
        [Column("orden_prioridad")]
        public int OrdenPrioridad { get; set; } = 1;

        /// <summary>Si false, el revisor no se considera (ej. ausencia temporal del jefe).</summary>
        [Column("active")]
        public bool Active { get; set; } = true;

        /// <summary>Soft delete: false = eliminado (se conserva para auditoría).</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
