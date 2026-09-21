using System.ComponentModel.DataAnnotations.Schema;

namespace Abril_Backend.Features.GestionAdministrativa.Shared.Models
{
    /// <summary>
    /// Aprobadores de la PRIMERA REVISIÓN de la planilla y firmantes del Consolidado del S10, por
    /// área (y por proyecto si el área filtra).
    ///
    /// Es gemela de <see cref="AreaRevisores"/> en forma y en cómo se recorre el árbol, pero
    /// responde otra pregunta: <c>area_revisores</c> quedó solo para <b>aprobar la salida</b>, y
    /// esta tabla para lo que pasa después con su planilla. Se separaron el 2026-09-21, cuando entró
    /// el ADMINISTRADOR DE OBRA al flujo: en obra la salida la aprueba el residente pero la planilla
    /// la revisa el administrador, así que un solo listado ya no podía decir las dos cosas.
    ///
    /// Lo que no tiene su gemela son las dos banderas: un área filtrada por proyecto lleva varios
    /// aprobadores a la vez y no todos intervienen en los dos pasos. Con más de una fila marcada,
    /// TODAS tienen que aprobar.
    /// </summary>
    [Table("area_revisores_rendicion")]
    public class AreaRevisoresRendicion
    {
        [Column("area_revisores_rendicion_id")]
        public int AreaRevisoresRendicionId { get; set; }

        /// <summary>Nodo del árbol de áreas (area_scope.area_scope_id) al que pertenecen.</summary>
        [Column("area_scope_id")]
        public int AreaScopeId { get; set; }

        /// <summary>
        /// Proyecto (project.project_id) cuando el área está marcada como "filtrar por proyecto"
        /// (ga_salidas_area_config.filtra_por_proyecto). NULL = a nivel de área.
        /// </summary>
        [Column("project_id")]
        public int? ProjectId { get; set; }

        /// <summary>Trabajador (workers.id) que aprueba/firma.</summary>
        [Column("revisor_id")]
        public int RevisorId { get; set; }

        /// <summary>
        /// 1 = primero. Acá no elige un ganador como en <see cref="AreaRevisores"/> —pueden hacer
        /// falta varios—, pero sí fija el ORDEN de las firmas del consolidado: primero el
        /// administrador de obra y después el residente.
        /// </summary>
        [Column("orden_prioridad")]
        public int OrdenPrioridad { get; set; } = 1;

        /// <summary>Su visto bueno hace falta en la primera revisión de la planilla.</summary>
        [Column("aprueba_primera_revision")]
        public bool ApruebaPrimeraRevision { get; set; } = true;

        /// <summary>Su firma hace falta en el Consolidado del S10 y su planilla grupal.</summary>
        [Column("aprueba_consolidado")]
        public bool ApruebaConsolidado { get; set; } = true;

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
