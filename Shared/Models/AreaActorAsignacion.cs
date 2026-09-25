using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// Lo personalizado "grupalmente por área" en Gestión Administrativa → Configuración → Revisores
    /// de Áreas: quién cumple un papel (<c>ga_actor</c>) para un tipo de trabajador
    /// (<c>ga_actor_caso</c>) de un área, y opcionalmente solo para una obra de esa área.
    ///
    /// Reemplaza a las tres tablas que decían lo mismo por separado (<c>area_revisores</c>,
    /// <c>area_revisores_rendicion</c> y <c>area_consolidadores</c>). Se sobrepone al algoritmo en su
    /// nodo, y a su vez lo personalizado para el trabajador (<see cref="WorkersActorAsignacion"/>) se
    /// sobrepone a esto.
    ///
    /// Con varias filas para el mismo (área, proyecto, actor, caso): en los actores de uno solo manda
    /// la primera activa por <see cref="OrdenPrioridad"/> y el resto queda de respaldo; en los de
    /// varios cuentan todas las activas, en ese orden.
    /// </summary>
    [Table("area_actor_asignacion")]
    public class AreaActorAsignacion
    {
        [Key]
        [Column("area_actor_asignacion_id")]
        public int AreaActorAsignacionId { get; set; }

        /// <summary>Nodo del árbol de áreas (area_scope.area_scope_id).</summary>
        [Column("area_scope_id")]
        public int AreaScopeId { get; set; }

        /// <summary>NULL = vale para toda el área; con valor = solo para los trabajadores de esa obra.</summary>
        [Column("project_id")]
        public int? ProjectId { get; set; }

        /// <summary>Papel asignado (<c>ActorIds</c>).</summary>
        [Column("ga_actor_id")]
        public int GaActorId { get; set; }

        /// <summary>Tipo de trabajador al que aplica (<c>ActorCasoIds</c>).</summary>
        [Column("ga_actor_caso_id")]
        public int GaActorCasoId { get; set; }

        /// <summary>Quién cumple el papel (workers.id).</summary>
        [Column("worker_id")]
        public int WorkerId { get; set; }

        /// <summary>1 = primero.</summary>
        [Column("orden_prioridad")]
        public int OrdenPrioridad { get; set; } = 1;

        /// <summary>false = no se considera (ausencia temporal; lo usa Delegación de Revisión).</summary>
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
