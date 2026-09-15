using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Consolidadores del S10 por área: quién, además del propio trabajador, puede adjuntar el
    /// Consolidado del S10 de sus planillas de rendición.
    ///
    /// Es el espejo de <see cref="AreaRevisores"/> —misma forma, mismo árbol, misma posibilidad de
    /// asignar por proyecto cuando el área filtra por proyecto— y se resuelve con el mismo
    /// algoritmo (<c>IConsolidadorResolver</c>, que comparte núcleo con <c>IJefeRevisorResolver</c>).
    /// La ÚNICA diferencia es cómo se lee la lista: en revisores gana el primer activo por
    /// prioridad, acá TODOS los activos del nodo que resuelve son aptos.
    ///
    /// Sin ninguna fila, el consolidador sale del algoritmo: el Jefe del área (o el Gerente, si el
    /// nodo es una gerencia; o el residente, si el nodo filtra por proyecto y es una obra).
    /// </summary>
    [Table("area_consolidadores")]
    public class AreaConsolidadores
    {
        [Column("area_consolidadores_id")]
        public int AreaConsolidadoresId { get; set; }

        /// <summary>Nodo del árbol de áreas (area_scope.area_scope_id) al que pertenecen.</summary>
        [Column("area_scope_id")]
        public int AreaScopeId { get; set; }

        /// <summary>
        /// Proyecto (project.project_id) cuando el área está marcada como "filtrar por proyecto"
        /// (ga_salidas_area_config.filtra_por_proyecto). NULL = consolidador a nivel de área, que
        /// por eso vale para todos los proyectos sin asignación propia.
        /// </summary>
        [Column("project_id")]
        public int? ProjectId { get; set; }

        /// <summary>Trabajador (workers.id) que puede consolidar por los del área.</summary>
        [Column("consolidador_id")]
        public int ConsolidadorId { get; set; }

        /// <summary>
        /// Orden de presentación (1 = primero). No decide quién gana —acá no gana uno solo— pero
        /// mantiene el mismo contrato que <see cref="AreaRevisores"/> y le da un orden estable a
        /// la lista.
        /// </summary>
        [Column("orden_prioridad")]
        public int OrdenPrioridad { get; set; } = 1;

        /// <summary>Si false, no se considera (ej. ausencia temporal).</summary>
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
