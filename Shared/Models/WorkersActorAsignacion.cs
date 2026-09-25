using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// Lo personalizado "individualmente por trabajador" en el formulario de trabajadores (Gestión de
    /// Ingresos): quién cumple un papel (<c>ga_actor</c>) sobre ESTE trabajador. Le gana a todo lo
    /// demás — a lo personalizado por área y al algoritmo —.
    ///
    /// Reemplaza a <c>workers_revisores</c> (el "jefe personalizado"), que solo cubría a quien aprueba
    /// la salida. Puede ser el propio trabajador: es una elección explícita de quien edita la ficha.
    /// </summary>
    [Table("workers_actor_asignacion")]
    public class WorkersActorAsignacion
    {
        [Key]
        [Column("workers_actor_asignacion_id")]
        public int WorkersActorAsignacionId { get; set; }

        /// <summary>Trabajador al que se le personaliza el papel (workers.id).</summary>
        [Column("worker_id")]
        public int WorkerId { get; set; }

        /// <summary>Papel (<c>ActorIds</c>).</summary>
        [Column("ga_actor_id")]
        public int GaActorId { get; set; }

        /// <summary>Quién lo cumple (workers.id).</summary>
        [Column("asignado_id")]
        public int AsignadoId { get; set; }

        /// <summary>1 = primero. En los actores de varios, es también el orden (el de las firmas).</summary>
        [Column("orden_prioridad")]
        public int OrdenPrioridad { get; set; } = 1;

        [Column("active")]
        public bool Active { get; set; } = true;

        /// <summary>Soft delete.</summary>
        [Column("state")]
        public bool State { get; set; } = true;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
