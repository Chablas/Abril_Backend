using System.ComponentModel.DataAnnotations.Schema;
using Abril_Backend.Features.ConfigurationModule.Features.AreaFeature.Infrastructure.Models;

namespace Abril_Backend.Shared.Models
{
    /// <summary>
    /// A qué área(s) pertenece un puesto. Un puesto puede estar en N áreas (CHOFER está en
    /// Logística y en Gerencia General); su <see cref="Categoria"/>, en cambio, sigue siendo
    /// una sola y vive en <c>puesto.categoria_id</c>.
    ///
    /// Su razón de ser es Solicitud de Personal: al solicitante se le ofrecen solo los puestos
    /// de su <c>area_scope</c> y de sus áreas hijas, en vez del catálogo completo. La data
    /// inicial salió del Excel de GTH y solo cubre personal de oficina, así que los puestos de
    /// obra no tienen ninguna fila acá — y eso es lo esperado, no un dato faltante.
    ///
    /// Migración: <c>Migrations_Manual/2026-08-21_puesto_area_scope.sql</c>.
    /// </summary>
    [Table("puesto_area_scope")]
    public class PuestoAreaScope
    {
        [Column("puesto_area_scope_id")]
        public int PuestoAreaScopeId { get; set; }

        [Column("puesto_id")]
        public int PuestoId { get; set; }

        [ForeignKey(nameof(PuestoId))]
        public Puesto? Puesto { get; set; }

        /// <summary>Nodo del árbol de áreas, no el <c>area_item</c>: el mismo nombre de área
        /// puede existir en varias ramas y lo que se filtra es la rama.</summary>
        [Column("area_scope_id")]
        public int AreaScopeId { get; set; }

        [ForeignKey(nameof(AreaScopeId))]
        public AreaScope? AreaScope { get; set; }

        [Column("created_date_time")]
        public DateTime CreatedDateTime { get; set; }

        [Column("created_user_id")]
        public int? CreatedUserId { get; set; }

        [Column("updated_date_time")]
        public DateTime? UpdatedDateTime { get; set; }

        [Column("updated_user_id")]
        public int? UpdatedUserId { get; set; }

        /// <summary>
        /// Soft-delete: false = el área se le quitó al puesto (la fila se conserva para el
        /// histórico). No hay <c>active</c> — para un vínculo no existe el caso "existe pero
        /// no aparece" —, y el índice único es parcial sobre las vivas, así que volver a
        /// asignar la misma área revive la fila en vez de duplicarla.
        /// </summary>
        [Column("state")]
        public bool State { get; set; } = true;
    }
}
